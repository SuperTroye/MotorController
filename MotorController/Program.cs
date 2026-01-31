using Gtk;
using MotorControllerApp;
using System.Device.Gpio;


IGpioController gpioController = OperatingSystem.IsWindows() ?
    new FakeGpioController() :
    new GpioControllerWrapper(new GpioController());

StepperMotorController stepperMotor = new StepperMotorController(gpioController, new StepperMotorSettings());

var application = Application.New("com.motorcontroller.app", Gio.ApplicationFlags.FlagsNone);
application.OnActivate += (sender, args) =>
{
    var window = Gtk.ApplicationWindow.New((Gtk.Application)sender);
    window.Title = "Motor Control";
    window.SetDefaultSize(800, 480);
    window.Resizable = false;

    window.Show();
};

return application.RunWithSynchronizationContext(null);


