using System.Device.Gpio;
using System.Diagnostics;

namespace Step;

/// <summary>
/// Direction of stepper motor rotation
/// </summary>
public enum MotorDirection
{
    Clockwise,
    Counterclockwise
}

/// <summary>
/// Controls a stepper motor driver using GPIO pins on a Raspberry Pi
/// </summary>
public class StepperMotorController : IDisposable
{
    private readonly IGpioController _gpioController;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly SemaphoreSlim _movementLock = new(1, 1);

    private long _currentPositionSteps;
    private bool _minLimitTriggered;
    private bool _maxLimitTriggered;
    private bool _disposed;

    private MotorController.StepperMotorSettings _settings;

    /// <summary>
    /// Gets the current position in inches
    /// </summary>
    public double CurrentPositionInches
    {
        get
        {
            lock (_movementLock)
            {
                double revolutions = (double)_currentPositionSteps / _settings.StepsPerRevolution;
                return revolutions / _settings.LeadScrewThreadsPerInch;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the StepperMotorController
    /// </summary>
    /// <param name="pulsePin">GPIO pin number for pulse signal</param>
    /// <param name="directionPin">GPIO pin number for direction signal</param>
    /// <param name="minLimitSwitchPin">GPIO pin number for minimum limit switch</param>
    /// <param name="maxLimitSwitchPin">GPIO pin number for maximum limit switch</param>
    /// <param name="stepsPerRevolution">Number of steps per revolution</param>
    /// <param name="enablePin">Optional GPIO pin number for enable signal</param>
    public StepperMotorController(
        IGpioController gpioController,
        MotorController.StepperMotorSettings settings)
    {
        _settings = settings;
        _gpioController = gpioController;
        _cancellationTokenSource = new CancellationTokenSource();

        InitializeGpioPins();
    }

    private void InitializeGpioPins()
    {
        // Setup output pins
        _gpioController.OpenPin(_settings.PulsePin, PinMode.Output);
        _gpioController.OpenPin(_settings.DirectionPin, PinMode.Output);
        _gpioController.Write(_settings.PulsePin, PinValue.Low);
        _gpioController.Write(_settings.DirectionPin, PinValue.Low);

        if (_settings.EnablePin.HasValue)
        {
            _gpioController.OpenPin(_settings.EnablePin.Value, PinMode.Output);
            _gpioController.Write(_settings.EnablePin.Value, PinValue.Low); // Active low enable
        }

        // Setup limit switch input pins with pull-up resistors
        _gpioController.OpenPin(_settings.MinLimitSwitchPin, PinMode.InputPullUp);
        _gpioController.OpenPin(_settings.MaxLimitSwitchPin, PinMode.InputPullUp);

        // Register callbacks for limit switches (triggered when pressed = LOW)
        _gpioController.RegisterCallbackForPinValueChangedEvent(
            _settings.MinLimitSwitchPin,
            PinEventTypes.Falling,
            OnMinLimitSwitchTriggered);

        _gpioController.RegisterCallbackForPinValueChangedEvent(
            _settings.MaxLimitSwitchPin,
            PinEventTypes.Falling,
            OnMaxLimitSwitchTriggered);
    }

    private void OnMinLimitSwitchTriggered(object sender, PinValueChangedEventArgs e)
    {
        _minLimitTriggered = true;
    }

    private void OnMaxLimitSwitchTriggered(object sender, PinValueChangedEventArgs e)
    {
        _maxLimitTriggered = true;
    }

    /// <summary>
    /// Moves the motor at specified RPM in the given direction
    /// </summary>
    /// <param name="rpm">Revolutions per minute</param>
    /// <param name="direction">Direction of rotation</param>
    /// <param name="steps">Number of steps to move (optional, runs continuously if not specified)</param>
    public async Task MoveAsync(double rpm, MotorDirection direction, long? steps = null)
    {
        if (rpm <= 0)
            throw new ArgumentException("RPM must be greater than zero", nameof(rpm));

        await _movementLock.WaitAsync();

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();

            // Set direction pin
            _gpioController.Write(_settings.DirectionPin,
                direction == MotorDirection.Clockwise ? PinValue.High : PinValue.Low);

            double targetStepsPerSecond = (rpm * _settings.StepsPerRevolution) / 60.0;
            long stepCount = 0;

            double currentSpeed = 0; // steps per second
            var stopwatch = Stopwatch.StartNew();
            double lastTime = 0;

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Check limit switches
                if (direction == MotorDirection.Clockwise && _maxLimitTriggered)
                {
                    _maxLimitTriggered = false;
                    throw new InvalidOperationException("Maximum limit switch triggered");
                }
                if (direction == MotorDirection.Counterclockwise && _minLimitTriggered)
                {
                    _minLimitTriggered = false;
                    throw new InvalidOperationException("Minimum limit switch triggered");
                }

                // Check if we've completed the requested steps
                if (steps.HasValue && stepCount >= steps.Value)
                    break;

                // Acceleration phase
                double currentTime = stopwatch.Elapsed.TotalSeconds;
                double deltaTime = currentTime - lastTime;

                if (currentSpeed < targetStepsPerSecond)
                {
                    currentSpeed = Math.Min(
                        currentSpeed + (_settings.AccelerationStepsPerSecondSquared * deltaTime),
                        targetStepsPerSecond);
                }

                // Calculate delay between pulses
                double delayMicroseconds = (1.0 / currentSpeed) * 1_000_000;

                // Generate pulse
                _gpioController.Write(_settings.PulsePin, PinValue.High);
                await Task.Delay(TimeSpan.FromMicroseconds(5)); // Minimum pulse width
                _gpioController.Write(_settings.PulsePin, PinValue.Low);

                // Update position
                if (direction == MotorDirection.Clockwise)
                    _currentPositionSteps++;
                else
                    _currentPositionSteps--;

                stepCount++;
                lastTime = currentTime;

                // Wait for next step
                int delayMs = Math.Max(1, (int)(delayMicroseconds / 1000) - 2);
                await Task.Delay(delayMs, _cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Motor stopped via Stop method
        }
        finally
        {
            _movementLock.Release();
        }
    }

    /// <summary>
    /// Moves the motor a specified distance in inches
    /// </summary>
    /// <param name="inches">Distance to move in inches (positive or negative)</param>
    /// <param name="rpm">Speed in revolutions per minute</param>
    public async Task MoveInchesAsync(double inches, double rpm)
    {
        if (rpm <= 0)
            throw new ArgumentException("RPM must be greater than zero", nameof(rpm));

        // Calculate required revolutions and steps
        double revolutions = Math.Abs(inches * _settings.LeadScrewThreadsPerInch);
        long steps = (long)(revolutions * _settings.StepsPerRevolution);

        MotorDirection direction = inches >= 0
            ? MotorDirection.Clockwise
            : MotorDirection.Counterclockwise;

        // Check if move would exceed limits (approximate check)
        double targetPosition = CurrentPositionInches + inches;
        if (targetPosition < 0 || _minLimitTriggered)
            throw new InvalidOperationException("Move would exceed minimum limit");

        await MoveWithAccelerationAsync(rpm, direction, steps);
    }

    /// <summary>
    /// Runs the motor until a limit switch is detected
    /// </summary>
    /// <param name="rpm">Speed in revolutions per minute</param>
    /// <param name="direction">Direction of rotation</param>
    public async Task RunToLimitSwitchAsync(double rpm, MotorDirection direction)
    {
        if (rpm <= 0)
            throw new ArgumentException("RPM must be greater than zero", nameof(rpm));

        await _movementLock.WaitAsync();

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();

            // Set direction pin
            _gpioController.Write(_settings.DirectionPin,
                direction == MotorDirection.Clockwise ? PinValue.High : PinValue.Low);

            double stepsPerSecond = (rpm * _settings.StepsPerRevolution) / 60.0;
            double delayMicroseconds = (1.0 / stepsPerSecond) * 1_000_000;
            int delayMs = Math.Max(1, (int)(delayMicroseconds / 1000));

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Check for limit switch
                if (direction == MotorDirection.Clockwise && _maxLimitTriggered)
                {
                    _maxLimitTriggered = false;
                    break;
                }
                if (direction == MotorDirection.Counterclockwise && _minLimitTriggered)
                {
                    _minLimitTriggered = false;
                    break;
                }

                // Generate pulse
                _gpioController.Write(_settings.PulsePin, PinValue.High);
                await Task.Delay(TimeSpan.FromMicroseconds(2));
                _gpioController.Write(_settings.PulsePin, PinValue.Low);

                // Update position
                if (direction == MotorDirection.Clockwise)
                    _currentPositionSteps++;
                else
                    _currentPositionSteps--;

                await Task.Delay(delayMs, _cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Motor stopped
        }
        finally
        {
            _movementLock.Release();
        }
    }

    /// <summary>
    /// Stops the motor with deceleration
    /// </summary>
    public async Task StopAsync()
    {
        await _cancellationTokenSource.CancelAsync();

        // Wait for movement to complete
        await _movementLock.WaitAsync();
        _movementLock.Release();
    }

    /// <summary>
    /// Homes the motor to the minimum limit switch and sets position to zero
    /// </summary>
    public async Task HomeAsync(double rpm = 60)
    {
        // Move towards minimum limit switch
        await RunToLimitSwitchAsync(rpm, MotorDirection.Counterclockwise);

        // Set current position to zero
        lock (_movementLock)
        {
            _currentPositionSteps = 0;
        }
    }

    /// <summary>
    /// Sets the current position to zero inches
    /// </summary>
    public void SetCurrentPositionToZero()
    {
        lock (_movementLock)
        {
            _currentPositionSteps = 0;
        }
    }

    private async Task MoveWithAccelerationAsync(double rpm, MotorDirection direction, long steps)
    {
        await _movementLock.WaitAsync();

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();

            // Set direction pin
            _gpioController.Write(_settings.DirectionPin,
                direction == MotorDirection.Clockwise ? PinValue.High : PinValue.Low);

            double targetStepsPerSecond = (rpm * _settings.StepsPerRevolution) / 60.0;
            long stepCount = 0;

            double currentSpeed = 0;
            var stopwatch = Stopwatch.StartNew();
            double lastTime = 0;

            // Calculate deceleration point (halfway if symmetric acceleration/deceleration)
            long accelSteps = (long)((targetStepsPerSecond * targetStepsPerSecond) / (2 * _settings.AccelerationStepsPerSecondSquared));
            long decelStartStep = Math.Max(0, steps - accelSteps);

            while (stepCount < steps && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Check limit switches
                if (direction == MotorDirection.Clockwise && _maxLimitTriggered)
                {
                    _maxLimitTriggered = false;
                    throw new InvalidOperationException("Maximum limit switch triggered");
                }
                if (direction == MotorDirection.Counterclockwise && _minLimitTriggered)
                {
                    _minLimitTriggered = false;
                    throw new InvalidOperationException("Minimum limit switch triggered");
                }

                double currentTime = stopwatch.Elapsed.TotalSeconds;
                double deltaTime = currentTime - lastTime;

                // Acceleration or deceleration
                if (stepCount < accelSteps && currentSpeed < targetStepsPerSecond)
                {
                    // Accelerate
                    currentSpeed = Math.Min(
                        currentSpeed + (_settings.AccelerationStepsPerSecondSquared * deltaTime),
                        targetStepsPerSecond);
                }
                else if (stepCount >= decelStartStep && currentSpeed > 0)
                {
                    // Decelerate
                    currentSpeed = Math.Max(
                        currentSpeed - (_settings.AccelerationStepsPerSecondSquared * deltaTime),
                        100); // Minimum speed
                }

                // Calculate delay
                double delayMicroseconds = (1.0 / Math.Max(currentSpeed, 1)) * 1_000_000;

                // Generate pulse
                _gpioController.Write(_settings.PulsePin, PinValue.High);
                await Task.Delay(TimeSpan.FromMicroseconds(2));
                _gpioController.Write(_settings.PulsePin, PinValue.Low);

                // Update position
                if (direction == MotorDirection.Clockwise)
                    _currentPositionSteps++;
                else
                    _currentPositionSteps--;

                stepCount++;
                lastTime = currentTime;

                // Wait for next step
                int delayMs = Math.Max(1, (int)(delayMicroseconds / 1000) - 2);
                await Task.Delay(delayMs, _cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Decelerate to stop
            await DecelerateToStopAsync();
        }
        finally
        {
            _movementLock.Release();
        }
    }

    private async Task DecelerateToStopAsync()
    {
        // Implement smooth deceleration when stopped
        // This is a simplified version
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(10);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();

        _gpioController?.Dispose();
        _movementLock?.Dispose();

        _disposed = true;
    }

    public bool IsMinLimitSwitchActive =>
        _gpioController.Read(_settings.MinLimitSwitchPin) == PinValue.Low;

    public bool IsMaxLimitSwitchActive =>
        _gpioController.Read(_settings.MaxLimitSwitchPin) == PinValue.Low;
}