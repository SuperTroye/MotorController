using System.Device.Gpio;
using MotorControllerApp;
using NSubstitute;
using Xunit;

namespace Test;

public class StepperMotorControllerTests
{
    private readonly IGpioController _mockGpioController;
    private readonly StepperMotorSettings _defaultSettings;

    public StepperMotorControllerTests()
    {
        _mockGpioController = Substitute.For<IGpioController>();
        _defaultSettings = new StepperMotorSettings
        {
            PulsePin = 1,
            DirectionPin = 2,
            EnablePin = 3,
            MinLimitSwitchPin = 4,
            MaxLimitSwitchPin = 5,
            StepsPerRevolution = 400,
            LeadScrewThreadsPerInch = 5,
            AccelerationStepsPerSecondSquared = 7000
        };

        // Setup default pin reads for limit switches (not triggered = HIGH)
        _mockGpioController.Read(_defaultSettings.MinLimitSwitchPin).Returns(PinValue.High);
        _mockGpioController.Read(_defaultSettings.MaxLimitSwitchPin).Returns(PinValue.High);
    }


}

