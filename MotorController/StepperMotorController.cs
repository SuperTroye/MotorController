using System.Device.Gpio;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MotorControllerApp;

public class StepperMotorController : IStepperMotorController
{
    private readonly IGpioController _gpio;
    private readonly ControllerConfig _config;
    private readonly ILogger<StepperMotorController> _logger;
    private readonly SemaphoreSlim _positionLock = new(1, 1);
    private CancellationTokenSource _stopTokenSource = new();
    private double _currentPositionSteps;
    private bool _disposed;
    private volatile bool _stopRequested;
    private double _targetRpm;

    public double CurrentPositionInches
    {
        get
        {
            _positionLock.Wait();
            try
            {
                return _currentPositionSteps / (int)_config.StepsPerRevolution / _config.LeadScrewThreadsPerInch;
            }
            finally
            {
                _positionLock.Release();
            }
        }
    }

    public bool IsMinLimitSwitchTriggered { get; private set; }
    public bool IsMaxLimitSwitchTriggered { get; private set; }

    public event EventHandler? MinLimitSwitchTriggered;
    public event EventHandler? MaxLimitSwitchTriggered;

    public StepperMotorController(IGpioController gpio, ControllerConfig config, ILogger<StepperMotorController> logger)
    {
        _gpio = gpio ?? throw new ArgumentNullException(nameof(gpio));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation("Initializing StepperMotorController with config: PulsePin={PulsePin}, DirectionPin={DirectionPin}, EnablePin={EnablePin}, MinLimitSwitchPin={MinLimitSwitchPin}, MaxLimitSwitchPin={MaxLimitSwitchPin}, StepsPerRevolution={StepsPerRevolution}, LeadScrewThreadsPerInch={LeadScrewThreadsPerInch}, Acceleration={Acceleration}",
            _config.PulsePin, _config.DirectionPin, _config.EnablePin, _config.MinLimitSwitchPin, _config.MaxLimitSwitchPin, _config.StepsPerRevolution, _config.LeadScrewThreadsPerInch, _config.Acceleration);

        InitializePins();
    }

    private void InitializePins()
    {
        // Close pins if already open (cleanup from previous runs)
        if (_gpio.IsPinOpen(_config.PulsePin)) _gpio.ClosePin(_config.PulsePin);
        if (_gpio.IsPinOpen(_config.DirectionPin)) _gpio.ClosePin(_config.DirectionPin);
        if (_config.EnablePin.HasValue && _gpio.IsPinOpen(_config.EnablePin.Value)) _gpio.ClosePin(_config.EnablePin.Value);
        if (_gpio.IsPinOpen(_config.MinLimitSwitchPin)) _gpio.ClosePin(_config.MinLimitSwitchPin);
        if (_gpio.IsPinOpen(_config.MaxLimitSwitchPin)) _gpio.ClosePin(_config.MaxLimitSwitchPin);

        // Initialize output pins
        _gpio.OpenPin(_config.PulsePin, PinMode.Output);
        _gpio.OpenPin(_config.DirectionPin, PinMode.Output);

        if (_config.EnablePin.HasValue)
        {
            _gpio.OpenPin(_config.EnablePin.Value, PinMode.Output);
            _gpio.Write(_config.EnablePin.Value, PinValue.High); // Disabled by default
        }

        // Initialize limit switch pins 
        _gpio.OpenPin(_config.MinLimitSwitchPin, PinMode.Input);
        _gpio.OpenPin(_config.MaxLimitSwitchPin, PinMode.Input);

        // Register callbacks for limit switches
        _gpio.RegisterCallbackForPinValueChangedEvent(_config.MinLimitSwitchPin, PinEventTypes.Falling | PinEventTypes.Rising, OnMinLimitSwitchChanged);
        _gpio.RegisterCallbackForPinValueChangedEvent(_config.MaxLimitSwitchPin, PinEventTypes.Falling | PinEventTypes.Rising, OnMaxLimitSwitchChanged);

        // Read initial limit switch states
        IsMinLimitSwitchTriggered = _gpio.Read(_config.MinLimitSwitchPin) == PinValue.Low;
        IsMaxLimitSwitchTriggered = _gpio.Read(_config.MaxLimitSwitchPin) == PinValue.Low;
    }

    private void OnMinLimitSwitchChanged(object sender, PinValueChangedEventArgs e)
    {
        IsMinLimitSwitchTriggered = e.ChangeType == PinEventTypes.Falling;
        _logger.LogInformation("Min limit switch {Status} (Pin {Pin})", IsMinLimitSwitchTriggered ? "triggered" : "released", _config.MinLimitSwitchPin);
        MinLimitSwitchTriggered?.Invoke(this, EventArgs.Empty);
    }

    private void OnMaxLimitSwitchChanged(object sender, PinValueChangedEventArgs e)
    {
        IsMaxLimitSwitchTriggered = e.ChangeType == PinEventTypes.Falling;
        _logger.LogInformation("Max limit switch {Status} (Pin {Pin})", IsMaxLimitSwitchTriggered ? "triggered" : "released", _config.MaxLimitSwitchPin);
        MaxLimitSwitchTriggered?.Invoke(this, EventArgs.Empty);
    }

    public async Task MoveInchesAsync(double inches, double rpm, CancellationToken cancellationToken = default)
    {
        if (rpm <= 0)
            throw new ArgumentException("RPM must be greater than zero", nameof(rpm));

        var totalSteps = (int)(inches * _config.LeadScrewThreadsPerInch * (int)_config.StepsPerRevolution);
        var direction = totalSteps >= 0;

        _gpio.Write(_config.DirectionPin, direction ? PinValue.High : PinValue.Low);

        if (_config.EnablePin.HasValue)
            _gpio.Write(_config.EnablePin.Value, PinValue.Low); // Enable motor

        try
        {
            await ExecuteMotionAsync(Math.Abs(totalSteps), rpm, direction, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when motion is cancelled via StopAsync or cancellationToken
        }
        finally
        {
            if (_config.EnablePin.HasValue)
                _gpio.Write(_config.EnablePin.Value, PinValue.High); // Disable motor
        }
    }

    public Task RunToLimitSwitchAsync(LimitSwitch direction, double rpm, CancellationToken cancellationToken = default)
    {
        if (rpm <= 0)
            throw new ArgumentException("RPM must be greater than zero", nameof(rpm));

        Interlocked.Exchange(ref _targetRpm, rpm); // Initialize target RPM

        bool toMaxLimit = direction == LimitSwitch.Max;
        _gpio.Write(_config.DirectionPin, toMaxLimit ? PinValue.Low : PinValue.High);

        if (_config.EnablePin.HasValue)
            _gpio.Write(_config.EnablePin.Value, PinValue.Low); // Enable motor

        return Task.Run(async () =>
        {
            try
            {
                var currentRpm = Interlocked.CompareExchange(ref _targetRpm, 0, 0); // Thread-safe read
                var maxStepsPerSecond = (currentRpm * (int)_config.StepsPerRevolution) / 60.0;
                var targetDelayMicroseconds = 1_000_000.0 / maxStepsPerSecond;

                // Initial delay using David Austin algorithm: c0 = 0.676 * sqrt(2/α) * 10^6
                var initialDelayMicroseconds = 0.676 * Math.Sqrt(2.0 / _config.Acceleration) * 1_000_000.0;

                // Calculate acceleration steps needed to reach target speed
                var accelerationSteps = (int)((maxStepsPerSecond * maxStepsPerSecond) / (2.0 * _config.Acceleration));

                // Limit deceleration to 300 steps maximum
                var decelerationSteps = Math.Min(accelerationSteps, 300);

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token);

                double delayMicroseconds = initialDelayMicroseconds;
                int step = 0;
                bool limitSwitchDetected = false;
                bool stopRequested = false;
                int decelStepCounter = 0;

                while (!linkedCts.Token.IsCancellationRequested)
                {
                    // Check if RPM changed and update timing if in constant speed phase
                    if (!limitSwitchDetected && !stopRequested && step >= accelerationSteps)
                    {
                        var latestRpm = Interlocked.CompareExchange(ref _targetRpm, 0, 0); // Thread-safe read
                        if (Math.Abs(currentRpm - latestRpm) > 0.01)
                        {
                            currentRpm = latestRpm;
                            maxStepsPerSecond = (currentRpm * (int)_config.StepsPerRevolution) / 60.0;
                            targetDelayMicroseconds = 1_000_000.0 / maxStepsPerSecond;
                            
                            // Recalculate deceleration steps for new speed
                            var newAccelerationSteps = (int)((maxStepsPerSecond * maxStepsPerSecond) / (2.0 * _config.Acceleration));
                            decelerationSteps = Math.Min(newAccelerationSteps, 300);
                            
                            _logger.LogInformation("Speed changed to {Rpm} RPM during motion (decel steps: {DecelSteps})", currentRpm, decelerationSteps);
                        }
                    }

                    // Check if stop was requested
                    if (!stopRequested && _stopRequested)
                    {
                        stopRequested = true;
                    }

                    // Check limit switch and start deceleration if detected
                    if (!limitSwitchDetected && !stopRequested)
                    {
                        if (toMaxLimit && IsMaxLimitSwitchTriggered)
                        {
                            limitSwitchDetected = true;
                        }
                        else if (!toMaxLimit && IsMinLimitSwitchTriggered)
                        {
                            limitSwitchDetected = true;
                        }
                    }

                    // If decelerating (due to limit switch or stop request) and reached the end, stop
                    if (limitSwitchDetected || stopRequested)
                    {
                        if (decelStepCounter >= decelerationSteps)
                            break;

                        // Deceleration phase - mirror the acceleration ramp
                        int decelStep = decelerationSteps - decelStepCounter;
                        if (decelStep > 0)
                        {
                            delayMicroseconds = delayMicroseconds + ((2.0 * delayMicroseconds) / ((4.0 * decelStep) - 1.0));
                        }
                        decelStepCounter++;
                    }
                    // Acceleration phase - David Austin algorithm: cn = cn-1 - (2 * cn-1) / (4n + 1)
                    else if (step < accelerationSteps)
                    {
                        if (step > 0)
                        {
                            delayMicroseconds = delayMicroseconds - ((2.0 * delayMicroseconds) / ((4.0 * step) + 1.0));
                        }
                    }
                    // Constant speed phase - maintain target speed
                    else
                    {
                        delayMicroseconds = targetDelayMicroseconds;
                    }

                    _gpio.Write(_config.PulsePin, PinValue.High);
                    MicrosecondSleep((int)delayMicroseconds / 2);

                    _gpio.Write(_config.PulsePin, PinValue.Low);
                    MicrosecondSleep((int)delayMicroseconds / 2);

                    await _positionLock.WaitAsync(linkedCts.Token);
                    try
                    {
                        _currentPositionSteps += toMaxLimit ? 1 : -1;
                    }
                    finally
                    {
                        _positionLock.Release();
                    }

                    step++;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopped
            }
            finally
            {
                _stopRequested = false;
                if (_config.EnablePin.HasValue)
                    _gpio.Write(_config.EnablePin.Value, PinValue.High); // Disable motor
            }
        });
    }

    public async Task StopAsync()
    {
        _stopRequested = true;
        await Task.CompletedTask;
    }


    /// <summary>
    /// Sets the target speed in revolutions per minute (RPM) while the motor is running.
    /// </summary>
    /// <remarks>
    /// This method allows dynamic speed adjustment during motion. The motor will smoothly transition
    /// to the new speed by recalculating the pulse timing. The new RPM will be applied during the
    /// constant speed phase of motion. Changes are ignored if RPM is zero or negative.
    /// </remarks>
    /// <param name="rpm">The target speed in revolutions per minute. Must be greater than zero.</param>
    public void SetTargetSpeed(double rpm)
    {
        if (rpm <= 0)
        {
            _logger.LogWarning("SetTargetRpm called with invalid RPM value: {Rpm}. Ignoring.", rpm);
            return;
        }

        Interlocked.Exchange(ref _targetRpm, rpm);
        _logger.LogInformation("Target RPM updated to {Rpm}", rpm);
    }

    public async Task ResetPositionAsync()
    {
        await _positionLock.WaitAsync();
        try
        {
            _currentPositionSteps = 0;
        }
        finally
        {
            _positionLock.Release();
        }
    }


    private void MicrosecondSleep(int microseconds)
    {
        // Get the frequency of the stopwatch (ticks per second)
        double stopwatchFrequency = Stopwatch.Frequency;

        // Calculate the number of ticks required for the desired delay
        long ticks = (long)(microseconds * (stopwatchFrequency / 1_000_000));

        // Get the start timestamp
        long start = Stopwatch.GetTimestamp();

        // Wait in a tight loop until the required ticks have elapsed
        while (Stopwatch.GetTimestamp() - start < ticks)
        {
            // Optional: use Thread.SpinWait to prevent the OS from unnecessarily
            // context switching for very short waits (e.g., < 40ns)
            // Thread.SpinWait(1); 
        }
    }


    /// <summary>
    /// Executes a motion sequence by generating step pulses with acceleration and deceleration profiles at the
    /// specified speed.
    /// </summary>
    /// <remarks>The motion sequence accelerates at the beginning, maintains a constant speed, and then
    /// decelerates before stopping. The operation can be cancelled at any time via the provided cancellation token or
    /// an internal stop request. If cancellation is requested, the motion will stop as soon as possible.</remarks>
    /// <param name="steps">The total number of steps to move. Must be a non-negative integer.</param>
    /// <param name="rpm">The target speed in revolutions per minute. Must be greater than zero.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the motion operation.</param>
    /// <param name="direction">The direction of movement. True for forward/max, false for reverse/min.</param>
    /// <returns>A task that represents the asynchronous operation of executing the motion sequence.</returns>
    internal Task ExecuteMotionAsync(int steps, double rpm, bool direction, CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _targetRpm, rpm); // Initialize target RPM
        var currentRpm = Interlocked.CompareExchange(ref _targetRpm, 0, 0); // Thread-safe read
        var maxStepsPerSecond = (currentRpm * (int)_config.StepsPerRevolution) / 60.0;
        var targetDelayMicroseconds = 1000000.0 / maxStepsPerSecond;

        // Initial delay using David Austin algorithm: c0 = 0.676 * sqrt(2/α) * 10^6
        var initialDelayMicroseconds = 0.676 * Math.Sqrt(2.0 / _config.Acceleration) * 1000000.0;

        // Calculate acceleration steps needed to reach target speed
        var accelerationSteps = (int)((maxStepsPerSecond * maxStepsPerSecond) / (2.0 * _config.Acceleration));
        var decelerationSteps = accelerationSteps;

        // Adjust if motion is too short for full acceleration/deceleration
        if (accelerationSteps + decelerationSteps > steps)
        {
            accelerationSteps = steps / 2;
            decelerationSteps = steps - accelerationSteps;
        }

        Debug.WriteLine($"Total Steps: {steps}");
        Debug.WriteLine($"Max steps/second: {maxStepsPerSecond:n2}");
        Debug.WriteLine($"Accel Steps: {accelerationSteps}");
        Debug.WriteLine($"Decel Steps: {decelerationSteps}");
        Debug.WriteLine($"Initial Delay us: {initialDelayMicroseconds}");
        Debug.WriteLine($"Target Delay us: {targetDelayMicroseconds}");

        return Task.Run(async () =>
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token);

            double delayMicroseconds = initialDelayMicroseconds;
            int accelStep = 0;
            bool stopRequested = false;
            int stepsWhenStopRequested = 0;

            for (int step = 0; step < steps && !linkedCts.Token.IsCancellationRequested; step++)
            {
                // Check if RPM changed and update timing if in constant speed phase
                if (!stopRequested && step >= accelerationSteps && step < steps - decelerationSteps)
                {
                    var latestRpm = Interlocked.CompareExchange(ref _targetRpm, 0, 0); // Thread-safe read
                    if (Math.Abs(currentRpm - latestRpm) > 0.01)
                    {
                        currentRpm = latestRpm;
                        maxStepsPerSecond = (currentRpm * (int)_config.StepsPerRevolution) / 60.0;
                        targetDelayMicroseconds = 1000000.0 / maxStepsPerSecond;
                        
                        // Recalculate deceleration steps for new speed
                        var newAccelerationSteps = (int)((maxStepsPerSecond * maxStepsPerSecond) / (2.0 * _config.Acceleration));
                        var newDecelerationSteps = newAccelerationSteps;
                        
                        // Adjust deceleration if not enough steps remaining
                        var stepsRemaining = steps - step;
                        if (newDecelerationSteps < stepsRemaining)
                        {
                            decelerationSteps = newDecelerationSteps;
                        }
                        else
                        {
                            // Not enough room for full deceleration, adjust to fit
                            decelerationSteps = Math.Max(1, stepsRemaining / 2);
                        }
                        
                        _logger.LogInformation("Speed changed to {Rpm} RPM during motion (step {Step}/{TotalSteps}, decel steps: {DecelSteps})", 
                            currentRpm, step, steps, decelerationSteps);
                    }
                }

                // Check if stop was requested - transition to deceleration
                if (!stopRequested && _stopRequested)
                {
                    stopRequested = true;
                    stepsWhenStopRequested = step;
                    // Adjust total steps to stop after deceleration completes
                    steps = Math.Min(steps, step + decelerationSteps);
                }

                // Acceleration phase - David Austin algorithm: cn = cn-1 - (2 * cn-1) / (4n + 1)
                if (!stopRequested && step < accelerationSteps)
                {
                    if (step > 0)
                    {
                        delayMicroseconds = delayMicroseconds - ((2.0 * delayMicroseconds) / ((4.0 * step) + 1.0));
                    }
                    accelStep = step + 1;
                }
                // Deceleration phase - mirror the acceleration ramp
                else if (step >= steps - decelerationSteps)
                {
                    int decelStep = steps - step;
                    if (decelStep > 0)
                    {
                        delayMicroseconds = delayMicroseconds + ((2.0 * delayMicroseconds) / ((4.0 * decelStep) - 1.0));
                    }
                }
                // Constant speed phase
                else
                {
                    delayMicroseconds = targetDelayMicroseconds;
                }

                //Debug.WriteLine($"Delay us: {delayMicroseconds}");

                _gpio.Write(_config.PulsePin, PinValue.High);
                MicrosecondSleep((int)delayMicroseconds / 2);

                _gpio.Write(_config.PulsePin, PinValue.Low);
                MicrosecondSleep((int)delayMicroseconds / 2);

                await _positionLock.WaitAsync(linkedCts.Token);
                try
                {
                    _currentPositionSteps += direction ? 1 : -1;
                }
                finally
                {
                    _positionLock.Release();
                }
            }

            _stopRequested = false;
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _stopTokenSource?.Cancel();
        _stopTokenSource?.Dispose();
        _positionLock?.Dispose();

        _gpio?.Dispose();

        _disposed = true;
    }
}