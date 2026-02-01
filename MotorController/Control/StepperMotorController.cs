using System.Device.Gpio;
using System.Diagnostics;

namespace MotorControllerApp;

public class StepperMotorController : IDisposable
{
    private readonly IGpioController _gpio;
    private readonly ControllerConfig _config;
    private readonly SemaphoreSlim _positionLock = new(1, 1);
    private CancellationTokenSource _stopTokenSource = new();
    private double _currentPositionSteps;
    private bool _isMinLimitTriggered;
    private bool _isMaxLimitTriggered;
    private bool _disposed;

    public double CurrentPositionInches
    {
        get
        {
            _positionLock.Wait();
            try
            {
                return _currentPositionSteps / _config.StepsPerRevolution / _config.LeadScrewThreadsPerInch;
            }
            finally
            {
                _positionLock.Release();
            }
        }
    }

    public bool IsMinLimitSwitchTriggered => _isMinLimitTriggered;
    public bool IsMaxLimitSwitchTriggered => _isMaxLimitTriggered;

    public StepperMotorController(IGpioController gpio, ControllerConfig config)
    {
        _gpio = gpio ?? throw new ArgumentNullException(nameof(gpio));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        InitializePins();
    }

    private void InitializePins()
    {
        // Initialize output pins
        _gpio.OpenPin(_config.PulsePin, PinMode.Output);
        _gpio.OpenPin(_config.DirectionPin, PinMode.Output);
        
        if (_config.EnablePin.HasValue)
        {
            _gpio.OpenPin(_config.EnablePin.Value, PinMode.Output);
            _gpio.Write(_config.EnablePin.Value, PinValue.High); // Disabled by default
        }

        // Initialize limit switch pins
        _gpio.OpenPin(_config.MinLimitSwitchPin, PinMode.InputPullUp);
        _gpio.OpenPin(_config.MaxLimitSwitchPin, PinMode.InputPullUp);

        // Register callbacks for limit switches
        _gpio.RegisterCallbackForPinValueChangedEvent(_config.MinLimitSwitchPin, PinEventTypes.Falling | PinEventTypes.Rising, OnMinLimitSwitchChanged);
        _gpio.RegisterCallbackForPinValueChangedEvent(_config.MaxLimitSwitchPin, PinEventTypes.Falling | PinEventTypes.Rising, OnMaxLimitSwitchChanged);

        // Read initial limit switch states
        _isMinLimitTriggered = _gpio.Read(_config.MinLimitSwitchPin) == PinValue.Low;
        _isMaxLimitTriggered = _gpio.Read(_config.MaxLimitSwitchPin) == PinValue.Low;
    }

    private void OnMinLimitSwitchChanged(object sender, PinValueChangedEventArgs e)
    {
        _isMinLimitTriggered = e.ChangeType == PinEventTypes.Falling;
    }

    private void OnMaxLimitSwitchChanged(object sender, PinValueChangedEventArgs e)
    {
        _isMaxLimitTriggered = e.ChangeType == PinEventTypes.Falling;
    }

    public async Task MoveInchesAsync(double inches, double rpm, CancellationToken cancellationToken = default)
    {
        if (rpm <= 0)
            throw new ArgumentException("RPM must be greater than zero", nameof(rpm));

        var totalSteps = (int)(inches * _config.LeadScrewThreadsPerInch * _config.StepsPerRevolution);
        var direction = totalSteps >= 0;

        // Check if move would exceed limits
        var newPosition = _currentPositionSteps + totalSteps;
        
        _gpio.Write(_config.DirectionPin, direction ? PinValue.High : PinValue.Low);

        if (_config.EnablePin.HasValue)
            _gpio.Write(_config.EnablePin.Value, PinValue.Low); // Enable motor

        try
        {
            await ExecuteMotionAsync(Math.Abs(totalSteps), rpm, cancellationToken);
            
            await _positionLock.WaitAsync(cancellationToken);
            try
            {
                _currentPositionSteps += totalSteps;
            }
            finally
            {
                _positionLock.Release();
            }
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

    public async Task RunToLimitSwitchAsync(bool toMaxLimit, double rpm, CancellationToken cancellationToken = default)
    {
        if (rpm <= 0)
            throw new ArgumentException("RPM must be greater than zero", nameof(rpm));

        _gpio.Write(_config.DirectionPin, toMaxLimit ? PinValue.High : PinValue.Low);

        if (_config.EnablePin.HasValue)
            _gpio.Write(_config.EnablePin.Value, PinValue.Low); // Enable motor

        try
        {
            var stepsPerSecond = (rpm * _config.StepsPerRevolution) / 60.0;
            var delayMicroseconds = (int)(1_000_000.0 / stepsPerSecond);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token);

            while (!linkedCts.Token.IsCancellationRequested)
            {
                // Check limit switch
                if (toMaxLimit && _isMaxLimitTriggered)
                    break;
                if (!toMaxLimit && _isMinLimitTriggered)
                    break;

                _gpio.Write(_config.PulsePin, PinValue.High);
                await Task.Delay(TimeSpan.FromMicroseconds(delayMicroseconds / 2), linkedCts.Token);
                _gpio.Write(_config.PulsePin, PinValue.Low);
                await Task.Delay(TimeSpan.FromMicroseconds(delayMicroseconds / 2), linkedCts.Token);

                await _positionLock.WaitAsync(linkedCts.Token);
                try
                {
                    _currentPositionSteps += toMaxLimit ? 1 : -1;
                }
                finally
                {
                    _positionLock.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopped
        }
        finally
        {
            if (_config.EnablePin.HasValue)
                _gpio.Write(_config.EnablePin.Value, PinValue.High); // Disable motor
        }
    }

    public async Task StopAsync()
    {
        _stopTokenSource.Cancel();
        _stopTokenSource.Dispose();
        _stopTokenSource = new CancellationTokenSource();
        await Task.CompletedTask;
    }

    public async Task HomeAsync(CancellationToken cancellationToken = default)
    {
        _gpio.Write(_config.DirectionPin, PinValue.Low); // Move to minimum

        if (_config.EnablePin.HasValue)
            _gpio.Write(_config.EnablePin.Value, PinValue.Low); // Enable motor

        try
        {
            var delayMicroseconds = 500; // Slow homing speed

            while (!_isMinLimitTriggered && !cancellationToken.IsCancellationRequested)
            {
                _gpio.Write(_config.PulsePin, PinValue.High);
                await Task.Delay(TimeSpan.FromMicroseconds(delayMicroseconds / 2), cancellationToken);
                _gpio.Write(_config.PulsePin, PinValue.Low);
                await Task.Delay(TimeSpan.FromMicroseconds(delayMicroseconds / 2), cancellationToken);
            }

            // Set position to zero
            await _positionLock.WaitAsync(cancellationToken);
            try
            {
                _currentPositionSteps = 0;
            }
            finally
            {
                _positionLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        finally
        {
            if (_config.EnablePin.HasValue)
                _gpio.Write(_config.EnablePin.Value, PinValue.High); // Disable motor
        }
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
    /// <returns>A task that represents the asynchronous operation of executing the motion sequence.</returns>
    private async Task ExecuteMotionAsync(int steps, double rpm, CancellationToken cancellationToken)
    {
        var maxStepsPerSecond = (rpm * _config.StepsPerRevolution) / 60.0;
        var targetDelayMicroseconds = 1_000_000.0 / maxStepsPerSecond;
        
        // Initial delay using David Austin algorithm: c0 = 0.676 * sqrt(2/α) * 10^6
        var initialDelayMicroseconds = 0.676 * Math.Sqrt(2.0 / _config.Acceleration) * 1_000_000.0;
        
        // Calculate acceleration steps needed to reach target speed
        var accelerationSteps = (int)((maxStepsPerSecond * maxStepsPerSecond) / (2 * _config.Acceleration));
        var decelerationSteps = accelerationSteps;
        
        // Adjust if motion is too short for full acceleration/deceleration
        if (accelerationSteps + decelerationSteps > steps)
        {
            accelerationSteps = steps / 2;
            decelerationSteps = steps - accelerationSteps;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token);

        double delayMicroseconds = initialDelayMicroseconds;
        int accelStep = 0;

        for (int step = 0; step < steps && !linkedCts.Token.IsCancellationRequested; step++)
        {
            // Acceleration phase - David Austin algorithm: cn = cn-1 - (2 * cn-1) / (4n + 1)
            if (step < accelerationSteps)
            {
                if (step > 0)
                {
                    delayMicroseconds = delayMicroseconds - (2.0 * delayMicroseconds) / (4.0 * accelStep + 1.0);
                }
                accelStep++;
            }
            // Deceleration phase - reverse the acceleration ramp
            else if (step >= steps - decelerationSteps)
            {
                accelStep--;
                if (accelStep > 0)
                {
                    delayMicroseconds = delayMicroseconds + (2.0 * delayMicroseconds) / (4.0 * accelStep + 1.0);
                }
            }
            // Constant speed phase
            else
            {
                delayMicroseconds = targetDelayMicroseconds;
            }

            // Ensure delay doesn't go below minimum
            delayMicroseconds = Math.Max(delayMicroseconds, targetDelayMicroseconds);

            _gpio.Write(_config.PulsePin, PinValue.High);
            await Task.Delay(TimeSpan.FromMicroseconds(delayMicroseconds / 2), linkedCts.Token);
            _gpio.Write(_config.PulsePin, PinValue.Low);
            await Task.Delay(TimeSpan.FromMicroseconds(delayMicroseconds / 2), linkedCts.Token);
        }
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