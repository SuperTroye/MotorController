using System.Device.Gpio;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MotorControllerApp;

/// <summary>
/// Controls two stepper motors — a linear axis and a synchronized rotary axis — in a single
/// step-generation thread, analogous to LinuxCNC's stepgen thread.
/// </summary>
/// <remarks>
/// <para>
/// The rotary axis is treated as if it were geared to the linear axis lead screw.
/// A DDA (Digital Differential Analyzer) accumulator advances the rotary motor by exactly
/// <c>GearRatio × (RotarySPR / LinearSPR)</c> rotary steps per linear step, with sub-step
/// residual carried forward so there is zero long-term drift.
/// </para>
/// <para>
/// Both motors share the same David Austin delay value at every iteration; the rotary axis
/// inherits the linear axis's acceleration and deceleration profile automatically.
/// Rotary pulses are fired within the same step period using stopwatch-anchored timing so the
/// linear pulse timing is never disturbed.
/// </para>
/// <para>
/// The rotary axis does not have limit switches because it rotates continuously.
/// </para>
/// </remarks>
public class SynchronizedDualAxisController : ISynchronizedDualAxisController
{
    private readonly IGpioController _gpio;
    private readonly SynchronizedDualAxisConfig _config;
    private readonly ILogger<SynchronizedDualAxisController> _logger;

    // --- linear axis state ---
    private readonly SemaphoreSlim _positionLock = new(1, 1);
    private readonly SemaphoreSlim _motionLock = new(1, 1);
    private CancellationTokenSource _stopTokenSource = new();
    private double _currentLinearPositionSteps;
    private bool _disposed;
    private volatile bool _stopRequested;
    private double _targetRpm;

    // --- rotary axis state ---
    private readonly SemaphoreSlim _rotaryPositionLock = new(1, 1);
    private double _currentRotaryPositionSteps;

    // -----------------------------------------------------------------------
    // Properties
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public double CurrentPositionInches
    {
        get
        {
            _positionLock.Wait();
            try
            {
                var linear = _config.LinearAxisConfig;
                return _currentLinearPositionSteps / (int)linear.StepsPerRevolution / linear.LeadScrewThreadsPerInch;
            }
            finally
            {
                _positionLock.Release();
            }
        }
    }

    /// <inheritdoc/>
    public double CurrentRotaryPositionDegrees
    {
        get
        {
            _rotaryPositionLock.Wait();
            try
            {
                return _currentRotaryPositionSteps / (int)_config.RotaryAxisConfig.StepsPerRevolution * 360.0;
            }
            finally
            {
                _rotaryPositionLock.Release();
            }
        }
    }

    /// <inheritdoc/>
    public bool IsMinLimitSwitchTriggered { get; private set; }

    /// <inheritdoc/>
    public bool IsMaxLimitSwitchTriggered { get; private set; }

    /// <inheritdoc/>
    public event EventHandler? MinLimitSwitchTriggered;

    /// <inheritdoc/>
    public event EventHandler? MaxLimitSwitchTriggered;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    /// <summary>
    /// Initializes a new instance of <see cref="SynchronizedDualAxisController"/>.
    /// </summary>
    /// <param name="gpio">GPIO controller (real or fake for development).</param>
    /// <param name="config">Synchronized dual-axis configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public SynchronizedDualAxisController(
        IGpioController gpio,
        SynchronizedDualAxisConfig config,
        ILogger<SynchronizedDualAxisController> logger)
    {
        _gpio = gpio ?? throw new ArgumentNullException(nameof(gpio));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var lin = _config.LinearAxisConfig;
        var rot = _config.RotaryAxisConfig;

        _logger.LogInformation(
            "Initializing SynchronizedDualAxisController | " +
            "Linear: PulsePin={LP}, DirPin={LD}, EnablePin={LE}, MinLimit={Mn}, MaxLimit={Mx}, SPR={LSPR}, TPI={TPI}, Accel={Accel} | " +
            "Rotary: PulsePin={RP}, DirPin={RD}, EnablePin={RE}, SPR={RSPR} | GearRatio={GR}",
            lin.PulsePin, lin.DirectionPin, lin.EnablePin, lin.MinLimitSwitchPin, lin.MaxLimitSwitchPin,
            lin.StepsPerRevolution, lin.LeadScrewThreadsPerInch, lin.Acceleration,
            rot.PulsePin, rot.DirectionPin, rot.EnablePin, rot.StepsPerRevolution,
            _config.GearRatio);

        InitializePins();
    }

    // -----------------------------------------------------------------------
    // GPIO initialisation
    // -----------------------------------------------------------------------

    private void InitializePins()
    {
        var lin = _config.LinearAxisConfig;
        var rot = _config.RotaryAxisConfig;

        // ---- linear axis ----
        CloseIfOpen(lin.PulsePin);
        CloseIfOpen(lin.DirectionPin);
        if (lin.EnablePin.HasValue) CloseIfOpen(lin.EnablePin.Value);
        CloseIfOpen(lin.MinLimitSwitchPin);
        CloseIfOpen(lin.MaxLimitSwitchPin);

        _gpio.OpenPin(lin.PulsePin, PinMode.Output);
        _gpio.OpenPin(lin.DirectionPin, PinMode.Output);

        if (lin.EnablePin.HasValue)
        {
            _gpio.OpenPin(lin.EnablePin.Value, PinMode.Output);
            _gpio.Write(lin.EnablePin.Value, PinValue.High); // Disabled by default
        }

        _gpio.OpenPin(lin.MinLimitSwitchPin, PinMode.Input);
        _gpio.OpenPin(lin.MaxLimitSwitchPin, PinMode.Input);

        _gpio.RegisterCallbackForPinValueChangedEvent(lin.MinLimitSwitchPin, PinEventTypes.Falling | PinEventTypes.Rising, OnMinLimitSwitchChanged);
        _gpio.RegisterCallbackForPinValueChangedEvent(lin.MaxLimitSwitchPin, PinEventTypes.Falling | PinEventTypes.Rising, OnMaxLimitSwitchChanged);

        IsMinLimitSwitchTriggered = _gpio.Read(lin.MinLimitSwitchPin) == PinValue.Low;
        IsMaxLimitSwitchTriggered = _gpio.Read(lin.MaxLimitSwitchPin) == PinValue.Low;

        // ---- rotary axis ----
        CloseIfOpen(rot.PulsePin);
        CloseIfOpen(rot.DirectionPin);
        if (rot.EnablePin.HasValue) CloseIfOpen(rot.EnablePin.Value);

        _gpio.OpenPin(rot.PulsePin, PinMode.Output);
        _gpio.OpenPin(rot.DirectionPin, PinMode.Output);

        if (rot.EnablePin.HasValue)
        {
            _gpio.OpenPin(rot.EnablePin.Value, PinMode.Output);
            _gpio.Write(rot.EnablePin.Value, PinValue.High); // Disabled by default
        }
    }

    private void CloseIfOpen(int pin)
    {
        if (_gpio.IsPinOpen(pin)) _gpio.ClosePin(pin);
    }

    // -----------------------------------------------------------------------
    // Limit switch callbacks
    // -----------------------------------------------------------------------

    private void OnMinLimitSwitchChanged(object sender, PinValueChangedEventArgs e)
    {
        IsMinLimitSwitchTriggered = e.ChangeType == PinEventTypes.Falling;
        _logger.LogInformation("Min limit switch {Status} (Pin {Pin})",
            IsMinLimitSwitchTriggered ? "triggered" : "released",
            _config.LinearAxisConfig.MinLimitSwitchPin);
        MinLimitSwitchTriggered?.Invoke(this, EventArgs.Empty);
    }

    private void OnMaxLimitSwitchChanged(object sender, PinValueChangedEventArgs e)
    {
        IsMaxLimitSwitchTriggered = e.ChangeType == PinEventTypes.Falling;
        _logger.LogInformation("Max limit switch {Status} (Pin {Pin})",
            IsMaxLimitSwitchTriggered ? "triggered" : "released",
            _config.LinearAxisConfig.MaxLimitSwitchPin);
        MaxLimitSwitchTriggered?.Invoke(this, EventArgs.Empty);
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task MoveInchesAsync(double inches, double rpm, CancellationToken cancellationToken = default)
    {
        if (rpm <= 0)
            throw new ArgumentException("RPM must be greater than zero", nameof(rpm));

        await _motionLock.WaitAsync(cancellationToken);
        try
        {
            _stopRequested = false;

            var lin = _config.LinearAxisConfig;
            var totalSteps = (int)(inches * lin.LeadScrewThreadsPerInch * (int)lin.StepsPerRevolution);
            bool forward = totalSteps >= 0;

            SetLinearDirection(forward);
            SetRotaryDirection(forward);
            EnableMotors();

            try
            {
                await ExecuteMotionAsync(Math.Abs(totalSteps), rpm, forward, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when motion is cancelled
            }
            finally
            {
                DisableMotors();
            }
        }
        finally
        {
            _motionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RunToLimitSwitchAsync(LimitSwitch direction, double rpm, CancellationToken cancellationToken = default)
    {
        if (rpm <= 0)
            throw new ArgumentException("RPM must be greater than zero", nameof(rpm));

        await _motionLock.WaitAsync(cancellationToken);
        try
        {
            _stopRequested = false;

            bool toMax = direction == LimitSwitch.Max;
            SetLinearDirection(toMax);
            SetRotaryDirection(toMax);
            EnableMotors();

            try
            {
                await ExecuteMotionInternalAsync(
                    direction: toMax,
                    initialRpm: rpm,
                    maxSteps: null,
                    shouldStartDeceleration: (step, accelSteps) =>
                    {
                        if (step < accelSteps) return false;
                        return toMax ? IsMaxLimitSwitchTriggered : IsMinLimitSwitchTriggered;
                    },
                    maxDecelerationSteps: 300,
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopped
            }
            finally
            {
                DisableMotors();
            }
        }
        finally
        {
            _motionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        _stopRequested = true;
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void SetTargetSpeed(double rpm)
    {
        if (rpm <= 0)
        {
            _logger.LogWarning("SetTargetRpm called with invalid RPM value: {Rpm}", rpm);
            return;
        }

        Interlocked.Exchange(ref _targetRpm, rpm);
        _logger.LogInformation("Target RPM updated to {Rpm}", rpm);
    }

    /// <inheritdoc/>
    public async Task ResetPositionAsync()
    {
        await _positionLock.WaitAsync();
        try
        {
            _currentLinearPositionSteps = 0;
        }
        finally
        {
            _positionLock.Release();
        }
    }

    // -----------------------------------------------------------------------
    // Enable / direction helpers
    // -----------------------------------------------------------------------

    private void SetLinearDirection(bool forward)
    {
        _gpio.Write(_config.LinearAxisConfig.DirectionPin, forward ? PinValue.Low : PinValue.High);
    }

    /// <summary>
    ///  Sets the rotary direction pin based on the desired linear direction and gear ratio.
    ///  Note: the rotary direction is inverted relative to the linear direction.
    /// </summary>
    /// <param name="forward"></param>
    private void SetRotaryDirection(bool forward)
    {
        _gpio.Write(_config.RotaryAxisConfig.DirectionPin, forward ? PinValue.High : PinValue.Low);
    }

    private void EnableMotors()
    {
        var lin = _config.LinearAxisConfig;
        var rot = _config.RotaryAxisConfig;

        if (lin.EnablePin.HasValue)
            _gpio.Write(lin.EnablePin.Value, PinValue.Low);

        if (rot.EnablePin.HasValue)
            _gpio.Write(rot.EnablePin.Value, PinValue.Low);
    }

    private void DisableMotors()
    {
        var lin = _config.LinearAxisConfig;
        var rot = _config.RotaryAxisConfig;

        if (lin.EnablePin.HasValue)
            _gpio.Write(lin.EnablePin.Value, PinValue.High);

        if (rot.EnablePin.HasValue)
            _gpio.Write(rot.EnablePin.Value, PinValue.High);
    }

    // -----------------------------------------------------------------------
    // Motion execution
    // -----------------------------------------------------------------------

    /// <summary>
    /// Executes a fixed-step motion sequence with full acceleration / deceleration profile.
    /// </summary>
    internal Task ExecuteMotionAsync(int steps, double rpm, bool direction, CancellationToken cancellationToken)
    {
        return ExecuteMotionInternalAsync(
            direction: direction,
            initialRpm: rpm,
            maxSteps: steps,
            shouldStartDeceleration: (_, __) => false,
            maxDecelerationSteps: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Unified motion execution engine that drives both the linear and rotary steppers in a single
    /// step-generation loop.
    /// </summary>
    /// <remarks>
    /// The rotary axis uses a DDA accumulator to fire the correct number of pulses per linear step
    /// without any long-term drift.  The per-step delay is identical for both axes; rotary pulses are
    /// inserted within the step period using stopwatch-anchored timing so the linear step timing is
    /// never disturbed.
    /// </remarks>
    private Task ExecuteMotionInternalAsync(
        bool direction,
        double initialRpm,
        int? maxSteps,
        Func<int, int, bool> shouldStartDeceleration,
        int? maxDecelerationSteps,
        CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _targetRpm, initialRpm);

        return Task.Run(async () =>
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token);

            var lin = _config.LinearAxisConfig;
            var rot = _config.RotaryAxisConfig;

            var currentRpm = Interlocked.CompareExchange(ref _targetRpm, 0, 0);
            var maxLinStepsPerSec = (currentRpm * (int)lin.StepsPerRevolution) / 60.0;
            var targetDelayMicroseconds = 1_000_000.0 / maxLinStepsPerSec;

            // David Austin initial delay: c0 = 0.676 * sqrt(2/α) * 10^6
            var initialDelayMicroseconds = 0.676 * Math.Sqrt(2.0 / lin.Acceleration) * 1_000_000.0;

            var accelerationSteps = (int)((maxLinStepsPerSec * maxLinStepsPerSec) / (2.0 * lin.Acceleration));
            var decelerationSteps = accelerationSteps;

            if (maxDecelerationSteps.HasValue)
                decelerationSteps = Math.Min(decelerationSteps, maxDecelerationSteps.Value);

            if (maxSteps.HasValue && accelerationSteps + decelerationSteps > maxSteps.Value)
            {
                accelerationSteps = maxSteps.Value / 2;
                decelerationSteps = maxSteps.Value - accelerationSteps;
            }

            // DDA: rotary steps per linear step = gearRatio × rotarySPR / linearSPR
            // Using an accumulator so the sub-step fraction is never lost → zero drift.
            double rotaryStepsPerLinearStep = _config.GearRatio
                * (int)rot.StepsPerRevolution
                / (double)(int)lin.StepsPerRevolution;

            if (maxSteps.HasValue)
            {
                _logger.LogDebug("Synchronized motion | LinearSteps={Steps}, MaxLinStepsPerSec={MaxSPS:n1}, " +
                    "AccelSteps={Accel}, DecelSteps={Decel}, InitDelay={Init:n1}µs, TargetDelay={Target:n1}µs, " +
                    "RotaryStepsPerLinearStep={Ratio:n4}",
                    maxSteps.Value, maxLinStepsPerSec, accelerationSteps, decelerationSteps,
                    initialDelayMicroseconds, targetDelayMicroseconds, rotaryStepsPerLinearStep);
            }

            double delayMicroseconds = initialDelayMicroseconds;
            double rotaryAccumulator = 0.0;
            bool stopRequested = false;
            bool externalDecelTrigger = false;
            int decelStepCounter = 0;
            int step = 0;

            while (true)
            {
                // Hard cancellation — emergency stop, throw immediately without deceleration
                if (linkedCts.Token.IsCancellationRequested)
                    linkedCts.Token.ThrowIfCancellationRequested();

                // Completed all requested steps?
                if (maxSteps.HasValue && step >= maxSteps.Value)
                    break;

                // Dynamic speed update while in constant-speed phase
                if (!externalDecelTrigger && !stopRequested && step >= accelerationSteps)
                {
                    var latestRpm = Interlocked.CompareExchange(ref _targetRpm, 0, 0);
                    if (Math.Abs(currentRpm - latestRpm) > 0.01)
                    {
                        currentRpm = latestRpm;
                        maxLinStepsPerSec = (currentRpm * (int)lin.StepsPerRevolution) / 60.0;
                        targetDelayMicroseconds = 1_000_000.0 / maxLinStepsPerSec;

                        var newDecelSteps = (int)((maxLinStepsPerSec * maxLinStepsPerSec) / (2.0 * lin.Acceleration));
                        if (maxDecelerationSteps.HasValue)
                            newDecelSteps = Math.Min(newDecelSteps, maxDecelerationSteps.Value);

                        if (maxSteps.HasValue)
                        {
                            var remaining = maxSteps.Value - step;
                            decelerationSteps = newDecelSteps < remaining ? newDecelSteps : Math.Max(1, remaining / 2);
                            _logger.LogInformation("Speed changed to {Rpm} RPM (step {Step}/{Total}, decel steps: {Decel})",
                                currentRpm, step, maxSteps.Value, decelerationSteps);
                        }
                        else
                        {
                            decelerationSteps = newDecelSteps;
                            _logger.LogInformation("Speed changed to {Rpm} RPM (decel steps: {Decel})", currentRpm, decelerationSteps);
                        }
                    }
                }

                // Graceful stop requested via StopAsync()
                if (!stopRequested && _stopRequested)
                {
                    stopRequested = true;

                    if (step < accelerationSteps)
                    {
                        decelerationSteps = Math.Max(1, step);
                        _logger.LogInformation("Stop during acceleration at step {Step} → decel for {Decel} steps.", step, decelerationSteps);
                    }
                    else
                    {
                        _logger.LogInformation("Stop during constant speed → decel for {Decel} steps.", decelerationSteps);
                    }
                }

                // External trigger (e.g. limit switch)
                if (!externalDecelTrigger && !stopRequested && shouldStartDeceleration(step, accelerationSteps))
                    externalDecelTrigger = true;

                // ---- compute delay for this step ----
                if (externalDecelTrigger || stopRequested)
                {
                    if (decelStepCounter >= decelerationSteps)
                        break;

                    int decelStep = decelerationSteps - decelStepCounter;
                    if (decelStep > 0)
                        delayMicroseconds += (2.0 * delayMicroseconds) / ((4.0 * decelStep) - 1.0);

                    decelStepCounter++;
                }
                else if (step < accelerationSteps)
                {
                    // Acceleration — David Austin: cn = cn-1 - (2·cn-1)/(4n+1)
                    if (step > 0)
                        delayMicroseconds -= (2.0 * delayMicroseconds) / ((4.0 * step) + 1.0);
                }
                else
                {
                    // Constant speed / planned deceleration for fixed-step moves
                    if (maxSteps.HasValue && step >= maxSteps.Value - decelerationSteps)
                    {
                        int decelStep = maxSteps.Value - step;
                        if (decelStep > 0)
                            delayMicroseconds += (2.0 * delayMicroseconds) / ((4.0 * decelStep) - 1.0);
                    }
                    else
                    {
                        delayMicroseconds = targetDelayMicroseconds;
                    }
                }

                _logger.LogDebug("Step {Step}: delay={Delay:n1}µs, rotaryAcc={Acc:n4}", step, delayMicroseconds, rotaryAccumulator);

                // ---- fire the linear pulse and any rotary pulses within one step period ----
                FireStepPeriod((int)delayMicroseconds, ref rotaryAccumulator, rotaryStepsPerLinearStep, direction);

                // ---- update positions (thread-safe) ----
                await _positionLock.WaitAsync(linkedCts.Token);
                try
                {
                    _currentLinearPositionSteps += direction ? 1 : -1;
                }
                finally
                {
                    _positionLock.Release();
                }

                step++;
            }

            _stopRequested = false;
        });
    }

    /// <summary>
    /// Executes one linear step pulse and all rotary step pulses that fall within its period.
    /// </summary>
    /// <remarks>
    /// The entire period of <paramref name="totalPeriodMicroseconds"/> is consumed here using
    /// stopwatch-anchored timing so the outer loop always advances at the correct rate.
    /// Rotary pulses are spaced evenly inside the period.
    /// </remarks>
    private void FireStepPeriod(int totalPeriodMicroseconds, ref double rotaryAccumulator, double rotaryStepsPerLinearStep, bool direction)
    {
        var rot = _config.RotaryAxisConfig;

        // Advance DDA accumulator
        rotaryAccumulator += rotaryStepsPerLinearStep;
        int rotaryPulsesThisStep = (int)rotaryAccumulator;
        rotaryAccumulator -= rotaryPulsesThisStep; // keep fractional remainder

        // Update rotary position counter (best-effort; not awaited here to keep timing tight)
        if (rotaryPulsesThisStep > 0)
        {
            _rotaryPositionLock.Wait();
            try
            {
                _currentRotaryPositionSteps += direction ? rotaryPulsesThisStep : -rotaryPulsesThisStep;
            }
            finally
            {
                _rotaryPositionLock.Release();
            }
        }

        long freq = Stopwatch.Frequency;
        long periodTicks = (long)(totalPeriodMicroseconds * (freq / 1_000_000.0));

        if (rotaryPulsesThisStep == 0)
        {
            // Simple case — just fire the linear pulse and consume the full period
            long start = Stopwatch.GetTimestamp();

            _gpio.Write(_config.LinearAxisConfig.PulsePin, PinValue.High);
            SpinUntil(start, periodTicks / 2);

            _gpio.Write(_config.LinearAxisConfig.PulsePin, PinValue.Low);
            SpinUntil(start, periodTicks);
        }
        else
        {
            // Interleaved case — divide period into (rotaryPulsesThisStep + 1) sub-slots.
            // Linear pulse fires at the start; rotary pulses are evenly spread within the window.
            long slotTicks = periodTicks / (rotaryPulsesThisStep + 1);
            long start = Stopwatch.GetTimestamp();

            // Linear HIGH for first half-slot
            _gpio.Write(_config.LinearAxisConfig.PulsePin, PinValue.High);
            SpinUntil(start, slotTicks / 2);

            _gpio.Write(_config.LinearAxisConfig.PulsePin, PinValue.Low);
            SpinUntil(start, slotTicks);

            // Rotary pulses fill the remaining slots
            for (int r = 0; r < rotaryPulsesThisStep; r++)
            {
                long slotStart = start + slotTicks * (r + 1);
                long halfSlot = slotTicks / 2;

                _gpio.Write(rot.PulsePin, PinValue.High);
                SpinUntil(slotStart, halfSlot);

                _gpio.Write(rot.PulsePin, PinValue.Low);
                SpinUntil(slotStart, slotTicks);
            }
        }
    }

    /// <summary>Busy-waits until <paramref name="elapsedTicks"/> have passed since <paramref name="origin"/>.</summary>
    private static void SpinUntil(long origin, long elapsedTicks)
    {
        while (Stopwatch.GetTimestamp() - origin < elapsedTicks) { }
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        _stopTokenSource?.Cancel();
        _stopTokenSource?.Dispose();
        _positionLock?.Dispose();
        _rotaryPositionLock?.Dispose();
        _motionLock?.Dispose();
        _gpio?.Dispose();

        _disposed = true;
    }
}
