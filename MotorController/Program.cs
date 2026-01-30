using Gtk;
using MotorController;
using MotorControllerApp;
using Step;
using System.Device.Gpio;



IGpioController gpioController = new GpioControllerWrapper(new GpioController());
StepperMotorController stepperMotor = new StepperMotorController(gpioController, new StepperMotorSettings());

var application = Application.New("com.motorcontroller.app", Gio.ApplicationFlags.FlagsNone);
application.OnActivate += (sender, args) =>
{
    var window = new MotorControlWindow(stepperMotor);
    window.Show();
};

application.Run();
