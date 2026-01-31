using System.Device.Gpio;

namespace MotorControllerApp;


public enum MotorDirection
{
    Clockwise,
    Counterclockwise
}


public class StepperMotorController : IDisposable
{
    private readonly IGpioController _gpioController;
    private readonly ControllerConfig _settings;
    
    public StepperMotorController(
        IGpioController gpioController,
        ControllerConfig settings)
    {
        _settings = settings;
        _gpioController = gpioController;
    }

    public void Dispose()
    {
    }
    

}