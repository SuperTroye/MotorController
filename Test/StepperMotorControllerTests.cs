using System.Device.Gpio;
using NSubstitute;
using Xunit;
using MotorControllerApp;

namespace MotorController.Tests;

public class StepperMotorControllerTests : IDisposable
{
    private readonly IGpioController _mockGpio;
    private readonly ControllerConfig _config;
    private readonly StepperMotorController _controller;

    public StepperMotorControllerTests()
    {
        _mockGpio = Substitute.For<IGpioController>();
        _config = new ControllerConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = 400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };

        // Setup default behavior for Read - limit switches not triggered
        _mockGpio.Read(_config.MinLimitSwitchPin).Returns(PinValue.High);
        _mockGpio.Read(_config.MaxLimitSwitchPin).Returns(PinValue.High);

        _controller = new StepperMotorController(_mockGpio, _config);
    }

    public void Dispose()
    {
        _controller?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenGpioIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StepperMotorController(null!, _config));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenConfigIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StepperMotorController(_mockGpio, null!));
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
        _mockGpio.Received(1).OpenPin(_config.MinLimitSwitchPin, PinMode.InputPullUp);
        _mockGpio.Received(1).OpenPin(_config.MaxLimitSwitchPin, PinMode.InputPullUp);
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
        var configWithEnable = new ControllerConfig
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
        using var controller = new StepperMotorController(mockGpio, configWithEnable);

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
        using var controller = new StepperMotorController(mockGpio, _config);

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
    public async Task MoveInchesAsync_ShouldSetDirectionHigh_WhenMovingPositive()
    {
        // Act
        await _controller.MoveInchesAsync(0.1, 60);

        // Assert
        _mockGpio.Received().Write(_config.DirectionPin, PinValue.High);
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldSetDirectionLow_WhenMovingNegative()
    {
        // Act
        await _controller.MoveInchesAsync(-0.1, 60);

        // Assert
        _mockGpio.Received().Write(_config.DirectionPin, PinValue.Low);
    }

    [Fact]
    public async Task MoveInchesAsync_ShouldEnableMotor_WhenEnablePinConfigured()
    {
        // Arrange
        var configWithEnable = new ControllerConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            EnablePin = 16,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = 400,
            LeadScrewThreadsPerInch = 5.0,
            Acceleration = 5000.0
        };

        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, configWithEnable);

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
        var shortConfig = new ControllerConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = 10, // Small for testing
            LeadScrewThreadsPerInch = 1.0,
            Acceleration = 50000.0 // High acceleration for quick test
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, shortConfig);

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
            _controller.RunToLimitSwitchAsync(true, 0));
    }

    [Fact]
    public async Task RunToLimitSwitchAsync_ShouldThrowArgumentException_WhenRpmIsNegative()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.RunToLimitSwitchAsync(true, -10));
    }

    [Fact]
    public async Task RunToLimitSwitchAsync_ShouldSetDirectionHigh_WhenMovingToMaxLimit()
    {
        // Arrange
        var cts = new CancellationTokenSource(100);

        // Act
        try
        {
            await _controller.RunToLimitSwitchAsync(true, 60, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockGpio.Received().Write(_config.DirectionPin, PinValue.High);
    }

    [Fact]
    public async Task RunToLimitSwitchAsync_ShouldSetDirectionLow_WhenMovingToMinLimit()
    {
        // Arrange
        var cts = new CancellationTokenSource(100);

        // Act
        try
        {
            await _controller.RunToLimitSwitchAsync(false, 60, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockGpio.Received().Write(_config.DirectionPin, PinValue.Low);
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

    #endregion

    #region HomeAsync Tests

    [Fact]
    public async Task HomeAsync_ShouldSetDirectionLow()
    {
        // Arrange
        var cts = new CancellationTokenSource(100);

        // Act
        try
        {
            await _controller.HomeAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockGpio.Received().Write(_config.DirectionPin, PinValue.Low);
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

        using var controller = new StepperMotorController(mockGpio, _config);

        // Simulate limit switch trigger
        var eventArgs = new PinValueChangedEventArgs(PinEventTypes.Falling, _config.MinLimitSwitchPin);
        minLimitCallback?.Invoke(null, eventArgs);

        // Act
        await controller.HomeAsync();

        // Assert
        Assert.Equal(0.0, controller.CurrentPositionInches);
    }

    #endregion

    #region ResetPositionAsync Tests

    [Fact]
    public async Task ResetPositionAsync_ShouldSetPositionToZero()
    {
        // Arrange - Move to a non-zero position first
        var shortConfig = new ControllerConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = 10,
            LeadScrewThreadsPerInch = 1.0,
            Acceleration = 50000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, shortConfig);

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
        var controller = new StepperMotorController(_mockGpio, _config);

        // Act
        controller.Dispose();

        // Assert
        _mockGpio.Received(1).Dispose();
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        var controller = new StepperMotorController(_mockGpio, _config);

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
        var shortConfig = new ControllerConfig
        {
            PulsePin = 21,
            DirectionPin = 20,
            MinLimitSwitchPin = 24,
            MaxLimitSwitchPin = 23,
            StepsPerRevolution = 10,
            LeadScrewThreadsPerInch = 1.0,
            Acceleration = 50000.0
        };
        var mockGpio = Substitute.For<IGpioController>();
        mockGpio.Read(Arg.Any<int>()).Returns(PinValue.High);
        using var controller = new StepperMotorController(mockGpio, shortConfig);

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
}