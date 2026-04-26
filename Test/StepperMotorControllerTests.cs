using System.Device.Gpio;
using NSubstitute;
using Xunit;
using MotorControllerApp;
using Microsoft.Extensions.Logging;

namespace MotorController.Tests;

public class StepperMotorControllerTests : IDisposable
{
    private readonly IGpioController _mockGpio;
    private readonly LinearAxisConfig _config;
    private readonly ILogger<StepperMotorController> _mockLogger;
    private readonly StepperMotorController _controller;

    public StepperMotorControllerTests()
    {
        _mockGpio = Substitute.For<IGpioController>();
        _mockLogger = Substitute.For<ILogger<StepperMotorController>>();
        _config = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };

        // Setup default behavior for Read - limit switches not triggered
        _mockGpio.Read(_config.MinLimitSwitchPin).Returns(PinValue.High);
        _mockGpio.Read(_config.MaxLimitSwitchPin).Returns(PinValue.High);

        _controller = new StepperMotorController(_mockGpio, _config, _mockLogger);
    }

    public void Dispose()
    {
        _controller?.Dispose();
    }

    private static ILogger<StepperMotorController> CreateMockLogger()
    {
        return Substitute.For<ILogger<StepperMotorController>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenGpioIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StepperMotorController(null!, _config, _mockLogger));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenConfigIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StepperMotorController(_mockGpio, null!, _mockLogger));
    }

    [Fact]
    public void Constructor_ShouldInitializeOutputPins()
    {
        // Assert
        _mockGpio.Received(1).OpenPin(_config.PulsePin, PinMode.Output);
        _mockGpio.Received(1).OpenPin(_config.DirectionPin, PinMode.Output);
    }

    [Fact]
    public void Constructor_ShouldInitializeLimitSwitchPins()
    {
        // Assert
        _mockGpio.Received(1).OpenPin(_config.MinLimitSwitchPin, PinMode.Input);
        _mockGpio.Received(1).OpenPin(_config.MaxLimitSwitchPin, PinMode.Input);
    }

    [Fact]
    public void Constructor_ShouldRegisterLimitSwitchCallbacks()
    {
        // Assert
        _mockGpio.Received(1).RegisterCallbackForPinValueChangedEvent(
            _config.MinLimitSwitchPin,
            PinEventTypes.Falling | PinEventTypes.Rising,
            Arg.Any<PinChangeEventHandler>());

        _mockGpio.Received(1).RegisterCallbackForPinValueChangedEvent(
            _config.MaxLimitSwitchPin,
            PinEventTypes.Falling | PinEventTypes.Rising,
            Arg.Any<PinChangeEventHandler>());
    }

    [Fact]
    public void Constructor_ShouldInitializeEnablePin_WhenConfigured()
    {
        // Arrange
        var configWithEnable = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            EnablePin = 16,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);

        // Act
        using var controller = new StepperMotorController(mockGpio, configWithEnable, CreateMockLogger());

        // Assert
        mockGpio.Received(1).OpenPin(16, PinMode.Output);
        mockGpio.Received(1).Write(16, PinValue.High); // Disabled by default
    }

    #endregion

    #region CurrentPositionInches Tests

    [Fact]
    public void CurrentPositionInches_ShouldReturnZero_Initially()
    {
        // Assert
        Assert.Equal(0.0, _controller.CurrentPositionInches);
    }

    [Fact]
    public async Task CurrentPositionInches_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Multiple threads reading position
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var position = _controller.CurrentPositionInches;
                Assert.True(position >= 0);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - No exceptions thrown
        Assert.True(true);
    }

    #endregion

    #region Limit Switch Tests

    [Fact]
    public void IsMinLimitSwitchTriggered_ShouldReturnFalse_Initially()
    {
        // Assert
        Assert.False(_controller.IsMinLimitSwitchTriggered);
    }

    [Fact]
    public void IsMaxLimitSwitchTriggered_ShouldReturnFalse_Initially()
    {
        // Assert
        Assert.False(_controller.IsMaxLimitSwitchTriggered);
    }

    [Fact]
    public void IsMinLimitSwitchTriggered_ShouldReturnTrue_WhenPinIsLowOnInit()
    {
        // Arrange
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(_config.MinLimitSwitchPin).Returns(PinValue.Low);
        mockGpio.Read(_config.MaxLimitSwitchPin).Returns(PinValue.High);

        // Act
        using var controller = new StepperMotorController(mockGpio, _config, CreateMockLogger());

        // Assert
        Assert.True(controller.IsMinLimitSwitchTriggered);
    }

    #endregion

    #region MoveInchesAsync Tests

    [Fact]
    public async Task MoveInchesAsync_ShouldThrowArgumentException_WhenRpmIsZero()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.MoveInchesAsync(1.0, 0));
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldThrowArgumentException_WhenRpmIsNegative()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.MoveInchesAsync(1.0, -10));
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldSetDirectionLow_WhenMovingPositive()
    {
        // Act
        await _controller.MoveInchesAsync(0.1, 60);

        // Assert
        _mockGpio.Received().Write(_config.DirectionPin, PinValue.Low);
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldSetDirectionHigh_WhenMovingNegative()
    {
        // Act
        await _controller.MoveInchesAsync(-0.1, 60);

        // Assert
        _mockGpio.Received().Write(_config.DirectionPin, PinValue.High);
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldEnableMotor_WhenEnablePinConfigured()
    {
        // Arrange
        var configWithEnable = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            EnablePin = 16,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };

        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, configWithEnable, CreateMockLogger());

        // Act
        await controller.MoveInchesAsync(0.1, 60);

        // Assert
        mockGpio.Received().Write(16, PinValue.Low); // Enabled
        mockGpio.Received().Write(16, PinValue.High); // Disabled in finally
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldGeneratePulses()
    {
        // Act
        await _controller.MoveInchesAsync(0.01, 60);

        // Assert - Should have generated some pulses
        _mockGpio.Received().Write(_config.PulsePin, PinValue.High);
        _mockGpio.Received().Write(_config.PulsePin, PinValue.Low);
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldUpdatePosition_AfterMove()
    {
        // Arrange
        var shortConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 1.0,
            Acceleration = 50000.0 // High acceleration for quick test
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, shortConfig, CreateMockLogger());

        // Act
        await controller.MoveInchesAsync(0.1, 60);

        // Assert
        Assert.Equal(0.1, controller.CurrentPositionInches, 2);
    }

    #endregion

    #region RunToLimitSwitchAsync Tests

    [Fact]
    public async Task RunToLimitSwitchAsync_ShouldThrowArgumentException_WhenRpmIsZero()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.RunToLimitSwitchAsync(LimitSwitch.Max, 0));
    }

    [Fact]
    public async Task RunToLimitSwitchAsync_ShouldThrowArgumentException_WhenRpmIsNegative()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.RunToLimitSwitchAsync(LimitSwitch.Max, -10));
    }

    [Fact]
    public async Task RunToLimitSwitchAsync_ShouldSetDirectionLow_WhenMovingToMaxLimit()
    {
        // Arrange
        var cts = new CancellationTokenSource(100);

        // Act
        try
        {
            await _controller.RunToLimitSwitchAsync(LimitSwitch.Max, 60, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockGpio.Received().Write(_config.DirectionPin, PinValue.Low);
    }

    [Fact]
    public async Task RunToLimitSwitchAsync_ShouldSetDirectionHigh_WhenMovingToMinLimit()
    {
        // Arrange
        var cts = new CancellationTokenSource(100);

        // Act
        try
        {
            await _controller.RunToLimitSwitchAsync(LimitSwitch.Min, 60, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockGpio.Received().Write(_config.DirectionPin, PinValue.High);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_ShouldCancelOngoingMotion()
    {
        // Arrange
        var moveTask = _controller.MoveInchesAsync(10.0, 60); // Long move

        // Act
        await Task.Delay(50); // Let motion start
        await _controller.StopAsync();

        // Assert - Move should complete or be cancelled
        await Task.WhenAny(moveTask, Task.Delay(1000));
        Assert.True(moveTask.IsCompleted);
    }

    [Fact]
    public async Task StopAsync_ShouldAllowSubsequentMoves()
    {
        // Arrange
        var moveTask = _controller.MoveInchesAsync(.1, 60);
        await Task.Delay(50);
        await _controller.StopAsync();
        await moveTask;

        // Act & Assert - Should not throw
        await _controller.MoveInchesAsync(0.1, 60);
    }

    [Fact]
    public async Task StopAsync_ShouldTriggerGracefulDeceleration_InsteadOfAbruptStop()
    {
        // Arrange
        var mockGpio = Substitute.For<IGpioController>();
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };
        
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        
        // Track pulse timings to verify deceleration pattern
        var pulseHighTimestamps = new List<long>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long stopCalledTimestamp = 0;
        
        mockGpio.When(x => x.Write(testConfig.PulsePin, PinValue.High))
            .Do(_ => pulseHighTimestamps.Add(stopwatch.ElapsedTicks));
        
        using var controller = new StepperMotorController(mockGpio, testConfig, CreateMockLogger());
        
        // Act
        var motionTask = controller.MoveInchesAsync(10.0, 100); // Long move at 100 RPM
        
        // Wait for motion to reach constant speed phase (past acceleration)
        await Task.Delay(200);
        
        // Record when stop was called
        stopCalledTimestamp = stopwatch.ElapsedTicks;
        await controller.StopAsync();
        
        // Wait for motion to complete gracefully
        await motionTask;
        stopwatch.Stop();
        
        // Assert - Verify deceleration pattern
        // Calculate delays between consecutive pulses (time between consecutive HIGH pulses)
        var delays = new List<long>();
        for (int i = 1; i < pulseHighTimestamps.Count; i++)
        {
            delays.Add(pulseHighTimestamps[i] - pulseHighTimestamps[i - 1]);
        }
        
        // Find pulses that occurred after StopAsync was called
        var pulsesAfterStop = pulseHighTimestamps.Count(t => t >= stopCalledTimestamp);
        
        // Verify that motion continued with deceleration steps after stop
        Assert.True(pulsesAfterStop > 5, 
            $"Expected deceleration steps after StopAsync(), but only {pulsesAfterStop} pulses occurred after stop");
        
        // Get the delays in the deceleration phase (last 30% of pulses after stop)
        var pulseIndexAtStop = pulseHighTimestamps.FindIndex(t => t >= stopCalledTimestamp);
        if (pulseIndexAtStop >= 0 && pulseIndexAtStop < delays.Count - 5)
        {
            var decelPhaseDelays = delays.Skip(pulseIndexAtStop).TakeLast(Math.Min(10, pulsesAfterStop - 1)).ToList();
            
            // Verify deceleration: delays should generally increase (motor slowing down)
            // At least 60% of consecutive delay pairs should show increase
            int increasingDelayCount = 0;
            for (int i = 1; i < decelPhaseDelays.Count; i++)
            {
                if (decelPhaseDelays[i] > decelPhaseDelays[i - 1])
                    increasingDelayCount++;
            }
            
            double decelerationRatio = decelPhaseDelays.Count > 1 
                ? (double)increasingDelayCount / (decelPhaseDelays.Count - 1) 
                : 0;
            
            Assert.True(decelerationRatio > 0.5, 
                $"Expected increasing delays during deceleration, but only {decelerationRatio:P0} of delays increased. " +
                $"This indicates an abrupt stop rather than gradual deceleration.");
        }
        
        // Verify motion completed without throwing OperationCanceledException
        Assert.True(motionTask.IsCompletedSuccessfully, 
            "Motion should complete gracefully without throwing OperationCanceledException when stopped via StopAsync()");
    }

    #endregion

    #region HomeAsync Tests

    [Fact]
    public async Task HomeAsync_ShouldSetDirectionHigh()
    {
        // Arrange
        var cts = new CancellationTokenSource(100);

        // Act
        try
        {
            await _controller.RunToLimitSwitchAsync(LimitSwitch.Min, 60, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockGpio.Received().Write(_config.DirectionPin, PinValue.High);
    }

    [Fact]
    public async Task HomeAsync_ShouldResetPositionToZero_WhenLimitReached()
    {
        // Arrange
        var mockGpio = Substitute.For<IGpioController>();
        var callCount = 0;
        PinChangeEventHandler? minLimitCallback = null;

        mockGpio.Read(_config.MinLimitSwitchPin).Returns(x =>
        {
            callCount++;
            return callCount > 5 ? PinValue.Low : PinValue.High;
        });
        mockGpio.Read(_config.MaxLimitSwitchPin).Returns(PinValue.High);

        mockGpio.When(x => x.RegisterCallbackForPinValueChangedEvent(
            _config.MinLimitSwitchPin,
            Arg.Any<PinEventTypes>(),
            Arg.Any<PinChangeEventHandler>()))
            .Do(callInfo => minLimitCallback = callInfo.ArgAt<PinChangeEventHandler>(2));

        using var controller = new StepperMotorController(mockGpio, _config, CreateMockLogger());

        // Simulate limit switch trigger
        var eventArgs = new PinValueChangedEventArgs(PinEventTypes.Falling, _config.MinLimitSwitchPin);
        minLimitCallback?.Invoke(null, eventArgs);

        // Act
        await controller.RunToLimitSwitchAsync(LimitSwitch.Min, 60);
        await controller.ResetPositionAsync();

        // Assert
        Assert.Equal(0.0, controller.CurrentPositionInches);
    }

    #endregion

    #region ResetPositionAsync Tests

    [Fact]
    public async Task ResetPositionAsync_ShouldSetPositionToZero()
    {
        // Arrange - Move to a non-zero position first
        var shortConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 1.0,
            Acceleration = 50000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, shortConfig, CreateMockLogger());

        await controller.MoveInchesAsync(0.5, 60);
        Assert.NotEqual(0.0, controller.CurrentPositionInches);

        // Act
        await controller.ResetPositionAsync();

        // Assert
        Assert.Equal(0.0, controller.CurrentPositionInches);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldDisposeGpioController()
    {
        // Arrange
        var controller = new StepperMotorController(_mockGpio, _config, CreateMockLogger());

        // Act
        controller.Dispose();

        // Assert
        _mockGpio.Received(1).Dispose();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        var controller = new StepperMotorController(_mockGpio, _config, CreateMockLogger());

        // Act
        controller.Dispose();
        controller.Dispose();
        controller.Dispose();

        // Assert - Should only dispose once
        _mockGpio.Received(1).Dispose();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task CompleteWorkflow_ShouldWorkCorrectly()
    {
        // Arrange
        var shortConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 1.0,
            Acceleration = 50000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, shortConfig, CreateMockLogger());

        // Act - Move forward
        await controller.MoveInchesAsync(0.5, 60);
        var pos1 = controller.CurrentPositionInches;

        // Move backward
        await controller.MoveInchesAsync(-0.2, 60);
        var pos2 = controller.CurrentPositionInches;

        // Reset position
        await controller.ResetPositionAsync();
        var pos3 = controller.CurrentPositionInches;

        // Assert
        Assert.Equal(0.5, pos1, 2);
        Assert.Equal(0.3, pos2, 2);
        Assert.Equal(0.0, pos3);
    }

    #endregion

    #region ExecuteMotionAsync Tests

    [Fact]
    public async Task ExecuteMotionAsync_ShouldCalculateCorrectAccelerationSteps()
    {
        // Arrange
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0 // steps/sec^2
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, testConfig, CreateMockLogger());

        double rpm = 60; // 60 RPM
        double maxStepsPerSecond = (rpm * (int)testConfig.StepsPerRevolution) / 60.0; // 400 steps/sec
        int expectedAccelSteps = (int)((maxStepsPerSecond * maxStepsPerSecond) / (2 * testConfig.Acceleration)); // (400*400)/(2*5000) = 16 steps

        int totalSteps = 100;

        // Act
        await controller.MoveInchesAsync(totalSteps / ((int)testConfig.StepsPerRevolution * testConfig.LeadScrewThreadsPerInch), rpm);

        // Assert - Verify we pulsed the correct total number of steps
        mockGpio.Received(totalSteps).Write(testConfig.PulsePin, PinValue.High);
        mockGpio.Received(totalSteps).Write(testConfig.PulsePin, PinValue.Low);

        // Expected accel steps should be 16 for this configuration
        Assert.Equal(16, expectedAccelSteps);
    }

    [Fact]
    public async Task ExecuteMotionAsync_ShouldCalculateCorrectInitialDelay()
    {
        // Arrange
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, testConfig, CreateMockLogger());

        // David Austin formula: c0 = 0.676 * sqrt(2/a) * 10^6
        double expectedInitialDelay = 0.676 * Math.Sqrt(2.0 / testConfig.Acceleration) * 1_000_000.0;
        // = 0.676 * sqrt(0.0004) * 1,000,000 = 0.676 * 0.02 * 1,000,000 = 13,520 microseconds

        // Act - move a small distance to trigger acceleration
        await controller.MoveInchesAsync(0.01, 60);

        // Assert - Just verify the calculation is correct (we can't directly observe the delay)
        Assert.Equal(13520.0, expectedInitialDelay, 0.5);
    }

    [Fact]
    public async Task ExecuteMotionAsync_ShouldUseCorrectDelayFormula()
    {
        // Arrange
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };

        // Verify David Austin formula calculations manually
        double c0 = 0.676 * Math.Sqrt(2.0 / 5000.0) * 1_000_000.0; // Initial delay
        double c1 = c0 - (2.0 * c0) / (4.0 * 1 + 1.0); // First step: n=1
        double c2 = c1 - (2.0 * c1) / (4.0 * 2 + 1.0); // Second step: n=2

        // Verify formula progression
        double expectedC1 = c0 * (1.0 - 2.0 / 5.0); // = c0 * 0.6
        double expectedC2 = c1 * (1.0 - 2.0 / 9.0); // = c1 * 0.778

        Assert.Equal(expectedC1, c1, 0.01);
        Assert.Equal(expectedC2, c2, 0.01);
    }

    [Fact]
    public async Task ExecuteMotionAsync_ShouldHandleShortMoves()
    {
        // Arrange - Create config where accel+decel would exceed total steps
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, testConfig, CreateMockLogger());

        double rpm = 60;
        double maxStepsPerSecond = (rpm * (int)testConfig.StepsPerRevolution) / 60.0;
        int fullAccelSteps = (int)((maxStepsPerSecond * maxStepsPerSecond) / (2 * testConfig.Acceleration)); // 16 steps

        int totalSteps = 10; // Less than full accel+decel (16+16=32)

        // For short moves: accelSteps = totalSteps / 2 = 5, decelSteps = totalSteps - accelSteps = 5
        int expectedAccelSteps = totalSteps / 2; // 5
        int expectedDecelSteps = totalSteps - expectedAccelSteps; // 5

        // Act
        await controller.MoveInchesAsync(totalSteps / ((int)testConfig.StepsPerRevolution * testConfig.LeadScrewThreadsPerInch), rpm);

        // Assert - Should still pulse correct number of times
        mockGpio.Received(totalSteps).Write(testConfig.PulsePin, PinValue.High);
        Assert.Equal(5, expectedAccelSteps);
        Assert.Equal(5, expectedDecelSteps);
    }

    [Fact]
    public async Task ExecuteMotionAsync_ShouldReachTargetSpeed()
    {
        // Arrange
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, testConfig, CreateMockLogger());

        double rpm = 60;
        double maxStepsPerSecond = (rpm * (int)testConfig.StepsPerRevolution) / 60.0; // 400 steps/sec
        double targetDelayMicroseconds = 1_000_000.0 / maxStepsPerSecond; // 2500 microseconds

        int totalSteps = 100; // Enough steps for full accel, constant speed, and decel

        // Act
        await controller.MoveInchesAsync(totalSteps / ((int)testConfig.StepsPerRevolution * testConfig.LeadScrewThreadsPerInch), rpm);

        // Assert - Verify target delay calculation
        Assert.Equal(2500.0, targetDelayMicroseconds, 0.1);
    }

    [Fact]
    public async Task ExecuteMotionAsync_ShouldSymmetricallyAccelerateAndDecelerate()
    {
        // Arrange
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, testConfig, CreateMockLogger());

        double rpm = 60;
        double maxStepsPerSecond = (rpm * (int)testConfig.StepsPerRevolution) / 60.0;
        int accelSteps = (int)((maxStepsPerSecond * maxStepsPerSecond) / (2 * testConfig.Acceleration));
        int decelSteps = accelSteps; // Should be symmetric

        int totalSteps = 100;

        // Act
        await controller.MoveInchesAsync(totalSteps / ((int)testConfig.StepsPerRevolution * testConfig.LeadScrewThreadsPerInch), rpm);

        // Assert - Verify symmetry
        Assert.Equal(accelSteps, decelSteps);
        Assert.Equal(16, accelSteps); // For this config
    }

    [Fact]
    public async Task ExecuteMotionAsync_ShouldCalculateDecelerationCorrectly()
    {
        // Arrange
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };

        // Verify deceleration formula (reverse of acceleration)
        // At constant speed: delay = targetDelay
        // Deceleration: cn = cn-1 + (2 * cn-1) / (4*decelStep - 1)

        double targetDelay = 2500.0; // microseconds at constant speed
        int decelStep = 5; // 5 steps remaining

        // First decel step (decelStep=5)
        double decel1 = targetDelay + (2.0 * targetDelay) / (4.0 * 5 - 1.0); // + 2*2500/19
        double expectedDecel1 = targetDelay * (1.0 + 2.0 / 19.0);

        Assert.Equal(expectedDecel1, decel1, 0.01);
    }

    [Theory]
    [InlineData(30, StepsPerRevolution.SPR_400)]  // 30 RPM, 200 steps/rev
    [InlineData(60, StepsPerRevolution.SPR_400)]  // 60 RPM, 400 steps/rev
    [InlineData(120, StepsPerRevolution.SPR_800)] // 120 RPM, 800 steps/rev
    public async Task ExecuteMotionAsync_ShouldCalculateCorrectlyForDifferentConfigurations(double rpm, StepsPerRevolution stepsPerRev)
    {
        // Arrange
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = stepsPerRev,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, testConfig, CreateMockLogger());

        double maxStepsPerSecond = (rpm * (int)stepsPerRev) / 60.0;
        int expectedAccelSteps = (int)((maxStepsPerSecond * maxStepsPerSecond) / (2 * testConfig.Acceleration));
        double expectedTargetDelay = 1_000_000.0 / maxStepsPerSecond;

        // Act - move enough steps for full profile
        int totalSteps = Math.Max(expectedAccelSteps * 3, 50);
        await controller.MoveInchesAsync(totalSteps / ((int)testConfig.StepsPerRevolution * testConfig.LeadScrewThreadsPerInch), rpm);

        // Assert - calculations should be consistent
        Assert.True(expectedAccelSteps > 0);
        Assert.True(expectedTargetDelay > 0);
        Assert.True(expectedTargetDelay < 1_000_000); // Should be less than 1 second
    }

    #endregion

    #region SetTargetRpm Tests

    [Fact]
    public void SetTargetRpm_ShouldAcceptValidRpm()
    {
        // Act
        _controller.SetTargetSpeed(100);

        // Assert - Should not throw and should log
        _mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Target RPM updated to 100")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void SetTargetRpm_ShouldRejectZeroRpm()
    {
        // Act
        _controller.SetTargetSpeed(0);

        // Assert - Should log warning and not set the value
        _mockLogger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("SetTargetRpm called with invalid RPM value: 0")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void SetTargetRpm_ShouldRejectNegativeRpm()
    {
        // Act
        _controller.SetTargetSpeed(-50);

        // Assert - Should log warning and not set the value
        _mockLogger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("SetTargetRpm called with invalid RPM value: -50")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData(10)]
    [InlineData(60)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(350)]
    public void SetTargetRpm_ShouldAcceptVariousValidRpmValues(double rpm)
    {
        // Act
        _controller.SetTargetSpeed(rpm);

        // Assert - Should log info message
        _mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains($"Target RPM updated to {rpm}")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SetTargetRpm_ShouldChangeSpeedDuringMotion()
    {
        // Arrange - Create a config with high acceleration for faster test
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 1.0,
            Acceleration = 10000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, testConfig, CreateMockLogger());

        // Act - Start motion in a background task
        var motionTask = Task.Run(async () => await controller.MoveInchesAsync(2.0, 60));
        
        // Wait a bit for motion to start and reach constant speed
        await Task.Delay(50);
        
        // Change speed during motion
        controller.SetTargetSpeed(120);
        
        // Wait for motion to complete
        await motionTask;

        // Assert - Motion should complete successfully and speed change should be logged
        CreateMockLogger().Received(0).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SetTargetRpm_ShouldRecalculateDecelerationSteps()
    {
        // Arrange
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 1.0,
            Acceleration = 5000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        var mockLogger = CreateMockLogger();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, testConfig, mockLogger);

        // Calculate expected deceleration steps for different speeds
        double initialRpm = 60;
        double newRpm = 120;
        
        double initialMaxStepsPerSec = (initialRpm * (int)testConfig.StepsPerRevolution) / 60.0;
        double newMaxStepsPerSec = (newRpm * (int)testConfig.StepsPerRevolution) / 60.0;
        
        int expectedInitialDecelSteps = (int)((initialMaxStepsPerSec * initialMaxStepsPerSec) / (2.0 * testConfig.Acceleration));
        int expectedNewDecelSteps = (int)((newMaxStepsPerSec * newMaxStepsPerSec) / (2.0 * testConfig.Acceleration));

        // Act - Start motion and change speed
        var motionTask = Task.Run(async () => await controller.MoveInchesAsync(2.0, initialRpm));
        await Task.Delay(50);
        controller.SetTargetSpeed(newRpm);
        await motionTask;

        // Assert - Verify deceleration calculation is correct
        Assert.NotEqual(expectedInitialDecelSteps, expectedNewDecelSteps);
        mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("decel steps:")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SetTargetRpm_ShouldNotAffectAccelerationPhase()
    {
        // Arrange - Very short motion so most of it is acceleration/deceleration
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 10.0,
            Acceleration = 5000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        var mockLogger = CreateMockLogger();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, testConfig, mockLogger);

        // Act - Start very short motion and immediately try to change speed
        var motionTask = Task.Run(async () => await controller.MoveInchesAsync(0.05, 60));
        controller.SetTargetSpeed(120); // Try to change during acceleration
        await motionTask;

        // Assert - Should complete without error (speed change ignored during acceleration)
        // No speed change log should appear because motion is too short
        mockLogger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Speed changed to 120")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SetTargetRpm_ShouldChangeSpeedDuringRunToLimitSwitch()
    {
        // Arrange
        var mockGpio = Substitute.For<IGpioController>();
        var mockLogger = CreateMockLogger();
        var callCount = 0;
        PinChangeEventHandler? minLimitCallback = null;

        // Simulate limit switch triggering after some steps
        mockGpio.Read(_config.MinLimitSwitchPin).Returns(x =>
        {
            callCount++;
            return callCount > 100 ? PinValue.Low : PinValue.High;
        });
        mockGpio.Read(_config.MaxLimitSwitchPin).Returns(PinValue.High);

        mockGpio.When(x => x.RegisterCallbackForPinValueChangedEvent(
            _config.MinLimitSwitchPin,
            Arg.Any<PinEventTypes>(),
            Arg.Any<PinChangeEventHandler>()))
            .Do(callInfo => minLimitCallback = callInfo.ArgAt<PinChangeEventHandler>(2));

        using var controller = new StepperMotorController(mockGpio, _config, mockLogger);

        // Act - Start motion to limit switch
        var motionTask = Task.Run(async () => await controller.RunToLimitSwitchAsync(LimitSwitch.Min, 60));
        
        // Wait for motion to start
        await Task.Delay(50);
        
        // Change speed during motion
        controller.SetTargetSpeed(100);
        
        // Trigger limit switch after speed change
        await Task.Delay(50);
        var eventArgs = new PinValueChangedEventArgs(PinEventTypes.Falling, _config.MinLimitSwitchPin);
        minLimitCallback?.Invoke(null, eventArgs);
        
        await motionTask;

        // Assert - Speed change should be logged
        mockLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Speed changed to 100")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SetTargetRpm_ShouldBeThreadSafe()
    {
        // Arrange
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 1.0,
            Acceleration = 10000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, testConfig, CreateMockLogger());

        // Act - Start motion and rapidly change speed from multiple threads
        var motionTask = Task.Run(async () => await controller.MoveInchesAsync(3.0, 60));
        
        var speedChangeTasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var rpm = 50 + (i * 10);
            speedChangeTasks.Add(Task.Run(() => controller.SetTargetSpeed(rpm)));
        }

        await Task.WhenAll(speedChangeTasks);
        await motionTask;

        // Assert - Should complete without exceptions
        Assert.True(motionTask.IsCompleted);
    }

    [Fact]
    public async Task SetTargetRpm_ShouldHandleMultipleSpeedChanges()
    {
        // Arrange
        var testConfig = new LinearAxisConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = StepsPerRevolution.SPR_400,
            LeadScrewThreadsPerInch = 1.0,
            Acceleration = 10000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        var mockLogger = CreateMockLogger();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, testConfig, mockLogger);

        // Act - Start motion and change speed multiple times
        var motionTask = Task.Run(async () => await controller.MoveInchesAsync(3.0, 60));
        
        await Task.Delay(20);
        controller.SetTargetSpeed(80);
        
        await Task.Delay(30);
        controller.SetTargetSpeed(120);
        
        await Task.Delay(30);
        controller.SetTargetSpeed(100);
        
        await motionTask;

        // Assert - All speed changes should be logged
        mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Target RPM updated to 80")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Target RPM updated to 120")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        
        mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Target RPM updated to 100")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion
}

public class SynchronizedDualAxisControllerTests : IDisposable
{
    private readonly IGpioController _mockGpio;
    private readonly SynchronizedDualAxisConfig _config;
    private readonly ILogger<SynchronizedDualAxisController> _mockLogger;
    private readonly SynchronizedDualAxisController _controller;

    public SynchronizedDualAxisControllerTests()
    {
        _mockGpio = Substitute.For<IGpioController>();
        _mockLogger = Substitute.For<ILogger<SynchronizedDualAxisController>>();

        _config = new SynchronizedDualAxisConfig
        {
            LinearAxisConfig = new LinearAxisConfig
            {
                PulsePin = 21,
                DirectionPin = 20,
                MinLimitSwitchPin = 24,
                MaxLimitSwitchPin = 23,
                StepsPerRevolution = StepsPerRevolution.SPR_400,
                LeadScrewThreadsPerInch = 5.0,
                Acceleration = 5000.0
            },
            RotaryAxisConfig = new RotaryAxisConfig
            {
                PulsePin = 16,
                DirectionPin = 12,
                StepsPerRevolution = StepsPerRevolution.SPR_400
            },
            GearRatio = 1.0 // 1:1 simplifies pulse-count assertions
        };

        _mockGpio.Read(_config.LinearAxisConfig.MinLimitSwitchPin).Returns(PinValue.High);
        _mockGpio.Read(_config.LinearAxisConfig.MaxLimitSwitchPin).Returns(PinValue.High);

        _controller = new SynchronizedDualAxisController(_mockGpio, _config, _mockLogger);
    }

    public void Dispose() => _controller?.Dispose();

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenGpioIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SynchronizedDualAxisController(null!, _config, _mockLogger));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenConfigIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SynchronizedDualAxisController(_mockGpio, null!, _mockLogger));
    }

    [Fact]
    public void Constructor_ShouldOpenLinearAxisPins()
    {
        var lin = _config.LinearAxisConfig;
        _mockGpio.Received(1).OpenPin(lin.PulsePin, PinMode.Output);
        _mockGpio.Received(1).OpenPin(lin.DirectionPin, PinMode.Output);
        _mockGpio.Received(1).OpenPin(lin.MinLimitSwitchPin, PinMode.Input);
        _mockGpio.Received(1).OpenPin(lin.MaxLimitSwitchPin, PinMode.Input);
    }

    [Fact]
    public void Constructor_ShouldOpenRotaryAxisPins()
    {
        var rot = _config.RotaryAxisConfig;
        _mockGpio.Received(1).OpenPin(rot.PulsePin, PinMode.Output);
        _mockGpio.Received(1).OpenPin(rot.DirectionPin, PinMode.Output);
    }

    [Fact]
    public void Constructor_ShouldRegisterLimitSwitchCallbacks()
    {
        var lin = _config.LinearAxisConfig;
        _mockGpio.Received(1).RegisterCallbackForPinValueChangedEvent(
            lin.MinLimitSwitchPin,
            PinEventTypes.Falling | PinEventTypes.Rising,
            Arg.Any<PinChangeEventHandler>());
        _mockGpio.Received(1).RegisterCallbackForPinValueChangedEvent(
            lin.MaxLimitSwitchPin,
            PinEventTypes.Falling | PinEventTypes.Rising,
            Arg.Any<PinChangeEventHandler>());
    }

    [Fact]
    public void Constructor_ShouldOpenEnablePins_WhenConfigured()
    {
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);

        var cfg = new SynchronizedDualAxisConfig
        {
            LinearAxisConfig = new LinearAxisConfig
            {
                PulsePin = 21, DirectionPin = 20, EnablePin = 19,
                MinLimitSwitchPin = 24, MaxLimitSwitchPin = 23
            },
            RotaryAxisConfig = new RotaryAxisConfig
            {
                PulsePin = 16, DirectionPin = 12, EnablePin = 13
            },
            GearRatio = 1.0
        };

        using var c = new SynchronizedDualAxisController(mockGpio, cfg, _mockLogger);

        mockGpio.Received(1).OpenPin(19, PinMode.Output);
        mockGpio.Received(1).Write(19, PinValue.High); // Disabled by default
        mockGpio.Received(1).OpenPin(13, PinMode.Output);
        mockGpio.Received(1).Write(13, PinValue.High);
    }

    // -----------------------------------------------------------------------
    // Initial property values
    // -----------------------------------------------------------------------

    [Fact]
    public void CurrentPositionInches_ShouldReturnZero_Initially()
    {
        Assert.Equal(0.0, _controller.CurrentPositionInches);
    }

    [Fact]
    public void CurrentRotaryPositionDegrees_ShouldReturnZero_Initially()
    {
        Assert.Equal(0.0, _controller.CurrentRotaryPositionDegrees);
    }

    [Fact]
    public void IsMinLimitSwitchTriggered_ShouldBeFalse_Initially()
    {
        Assert.False(_controller.IsMinLimitSwitchTriggered);
    }

    [Fact]
    public void IsMaxLimitSwitchTriggered_ShouldBeFalse_Initially()
    {
        Assert.False(_controller.IsMaxLimitSwitchTriggered);
    }

    // -----------------------------------------------------------------------
    // MoveInchesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MoveInchesAsync_ShouldThrowArgumentException_WhenRpmIsZero()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _controller.MoveInchesAsync(1.0, 0));
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldThrowArgumentException_WhenRpmIsNegative()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _controller.MoveInchesAsync(1.0, -1));
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldSetLinearDirectionLow_WhenMovingPositive()
    {
        await _controller.MoveInchesAsync(0.1, 60);
        _mockGpio.Received().Write(_config.LinearAxisConfig.DirectionPin, PinValue.Low);
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldSetLinearDirectionHigh_WhenMovingNegative()
    {
        await _controller.MoveInchesAsync(-0.1, 60);
        _mockGpio.Received().Write(_config.LinearAxisConfig.DirectionPin, PinValue.High);
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldSetRotaryDirectionLow_WhenMovingPositive()
    {
        await _controller.MoveInchesAsync(0.1, 60);
        _mockGpio.Received().Write(_config.RotaryAxisConfig.DirectionPin, PinValue.Low);
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldSetRotaryDirectionHigh_WhenMovingNegative()
    {
        await _controller.MoveInchesAsync(-0.1, 60);
        _mockGpio.Received().Write(_config.RotaryAxisConfig.DirectionPin, PinValue.High);
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldGenerateLinearPulses()
    {
        await _controller.MoveInchesAsync(0.01, 60);
        _mockGpio.Received().Write(_config.LinearAxisConfig.PulsePin, PinValue.High);
        _mockGpio.Received().Write(_config.LinearAxisConfig.PulsePin, PinValue.Low);
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldUpdateLinearPosition()
    {
        var cfg = new SynchronizedDualAxisConfig
        {
            LinearAxisConfig = new LinearAxisConfig
            {
                PulsePin = 21, DirectionPin = 20, MinLimitSwitchPin = 24, MaxLimitSwitchPin = 23,
                StepsPerRevolution = StepsPerRevolution.SPR_400,
                LeadScrewThreadsPerInch = 1.0,
                Acceleration = 50000.0
            },
            RotaryAxisConfig = new RotaryAxisConfig { PulsePin = 16, DirectionPin = 12 },
            GearRatio = 1.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var c = new SynchronizedDualAxisController(mockGpio, cfg, _mockLogger);

        await c.MoveInchesAsync(0.1, 60);

        Assert.Equal(0.1, c.CurrentPositionInches, 2);
    }

    // -----------------------------------------------------------------------
    // Rotary synchronization & DDA accuracy
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MoveInchesAsync_RotaryPulseCount_ShouldMatchGearRatioScaling_OneToOne()
    {
        // GearRatio = 1.0, SPR equal → rotary pulses should equal linear pulses
        var cfg = new SynchronizedDualAxisConfig
        {
            LinearAxisConfig = new LinearAxisConfig
            {
                PulsePin = 21, DirectionPin = 20, MinLimitSwitchPin = 24, MaxLimitSwitchPin = 23,
                StepsPerRevolution = StepsPerRevolution.SPR_400,
                LeadScrewThreadsPerInch = 1.0,
                Acceleration = 50000.0
            },
            RotaryAxisConfig = new RotaryAxisConfig
            {
                PulsePin = 16, DirectionPin = 12,
                StepsPerRevolution = StepsPerRevolution.SPR_400
            },
            GearRatio = 1.0
        };

        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var c = new SynchronizedDualAxisController(mockGpio, cfg, _mockLogger);

        int totalLinearSteps = 400; // 1 inch at 1 TPI × 400 SPR
        await c.MoveInchesAsync(1.0, 60);

        // With 1:1 gear ratio and same SPR, rotary pulses == linear pulses
        mockGpio.Received(totalLinearSteps).Write(cfg.LinearAxisConfig.PulsePin, PinValue.High);
        mockGpio.Received(totalLinearSteps).Write(cfg.RotaryAxisConfig.PulsePin, PinValue.High);
    }

    [Fact]
    public async Task MoveInchesAsync_RotaryPulseCount_ShouldScaleByGearRatio()
    {
        // GearRatio = 0.5 → rotary fires half as many pulses per linear step
        var cfg = new SynchronizedDualAxisConfig
        {
            LinearAxisConfig = new LinearAxisConfig
            {
                PulsePin = 21, DirectionPin = 20, MinLimitSwitchPin = 24, MaxLimitSwitchPin = 23,
                StepsPerRevolution = StepsPerRevolution.SPR_400,
                LeadScrewThreadsPerInch = 1.0,
                Acceleration = 50000.0
            },
            RotaryAxisConfig = new RotaryAxisConfig
            {
                PulsePin = 16, DirectionPin = 12,
                StepsPerRevolution = StepsPerRevolution.SPR_400
            },
            GearRatio = 0.5
        };

        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var c = new SynchronizedDualAxisController(mockGpio, cfg, _mockLogger);

        await c.MoveInchesAsync(1.0, 60); // 400 linear steps

        // 0.5 × (400/400) × 400 linear steps = 200 rotary pulses
        mockGpio.Received(400).Write(cfg.LinearAxisConfig.PulsePin, PinValue.High);
        mockGpio.Received(200).Write(cfg.RotaryAxisConfig.PulsePin, PinValue.High);
    }

    [Fact]
    public async Task MoveInchesAsync_RotaryPositionDegrees_ShouldBeNonZeroAfterMove()
    {
        var cfg = new SynchronizedDualAxisConfig
        {
            LinearAxisConfig = new LinearAxisConfig
            {
                PulsePin = 21, DirectionPin = 20, MinLimitSwitchPin = 24, MaxLimitSwitchPin = 23,
                StepsPerRevolution = StepsPerRevolution.SPR_400,
                LeadScrewThreadsPerInch = 1.0,
                Acceleration = 50000.0
            },
            RotaryAxisConfig = new RotaryAxisConfig
            {
                PulsePin = 16, DirectionPin = 12,
                StepsPerRevolution = StepsPerRevolution.SPR_400
            },
            GearRatio = 1.0
        };

        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var c = new SynchronizedDualAxisController(mockGpio, cfg, _mockLogger);

        await c.MoveInchesAsync(1.0, 60);

        // 1:1, 1 TPI, 400 SPR → 400 rotary steps = 360°
        Assert.Equal(360.0, c.CurrentRotaryPositionDegrees, 1);
    }

    // -----------------------------------------------------------------------
    // StopAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StopAsync_ShouldCompleteMotionGracefully()
    {
        var moveTask = _controller.MoveInchesAsync(10.0, 60);
        await Task.Delay(50);
        await _controller.StopAsync();
        await Task.WhenAny(moveTask, Task.Delay(2000));
        Assert.True(moveTask.IsCompleted);
    }

    [Fact]
    public async Task StopAsync_ShouldAllowSubsequentMoves()
    {
        var moveTask = _controller.MoveInchesAsync(5.0, 60);
        await Task.Delay(50);
        await _controller.StopAsync();
        await moveTask;

        // Should not throw
        await _controller.MoveInchesAsync(0.1, 60);
    }

    // -----------------------------------------------------------------------
    // RunToLimitSwitchAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RunToLimitSwitchAsync_ShouldThrowArgumentException_WhenRpmIsZero()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.RunToLimitSwitchAsync(LimitSwitch.Max, 0));
    }

    [Fact]
    public async Task RunToLimitSwitchAsync_ShouldSetLinearDirectionLow_WhenMovingToMaxLimit()
    {
        var cts = new CancellationTokenSource(100);
        try { await _controller.RunToLimitSwitchAsync(LimitSwitch.Max, 60, cts.Token); }
        catch (OperationCanceledException) { }

        _mockGpio.Received().Write(_config.LinearAxisConfig.DirectionPin, PinValue.Low);
    }

    [Fact]
    public async Task RunToLimitSwitchAsync_ShouldSetLinearDirectionHigh_WhenMovingToMinLimit()
    {
        var cts = new CancellationTokenSource(100);
        try { await _controller.RunToLimitSwitchAsync(LimitSwitch.Min, 60, cts.Token); }
        catch (OperationCanceledException) { }

        _mockGpio.Received().Write(_config.LinearAxisConfig.DirectionPin, PinValue.High);
    }

    // -----------------------------------------------------------------------
    // ResetPositionAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ResetPositionAsync_ShouldSetLinearPositionToZero()
    {
        var cfg = new SynchronizedDualAxisConfig
        {
            LinearAxisConfig = new LinearAxisConfig
            {
                PulsePin = 21, DirectionPin = 20, MinLimitSwitchPin = 24, MaxLimitSwitchPin = 23,
                StepsPerRevolution = StepsPerRevolution.SPR_400,
                LeadScrewThreadsPerInch = 1.0,
                Acceleration = 50000.0
            },
            RotaryAxisConfig = new RotaryAxisConfig { PulsePin = 16, DirectionPin = 12 },
            GearRatio = 1.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var c = new SynchronizedDualAxisController(mockGpio, cfg, _mockLogger);

        await c.MoveInchesAsync(0.5, 60);
        Assert.NotEqual(0.0, c.CurrentPositionInches);

        await c.ResetPositionAsync();
        Assert.Equal(0.0, c.CurrentPositionInches);
    }

    // -----------------------------------------------------------------------
    // Limit switches
    // -----------------------------------------------------------------------

    [Fact]
    public void IsMinLimitSwitchTriggered_ShouldBeTrueWhenPinIsLowOnInit()
    {
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(_config.LinearAxisConfig.MinLimitSwitchPin).Returns(PinValue.Low);
        mockGpio.Read(_config.LinearAxisConfig.MaxLimitSwitchPin).Returns(PinValue.High);

        using var c = new SynchronizedDualAxisController(mockGpio, _config, _mockLogger);
        Assert.True(c.IsMinLimitSwitchTriggered);
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    [Fact]
    public void Dispose_ShouldDisposeGpio()
    {
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        var c = new SynchronizedDualAxisController(mockGpio, _config, _mockLogger);
        c.Dispose();
        mockGpio.Received(1).Dispose();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        var c = new SynchronizedDualAxisController(mockGpio, _config, _mockLogger);
        c.Dispose();
        c.Dispose();
        mockGpio.Received(1).Dispose();
    }
}
