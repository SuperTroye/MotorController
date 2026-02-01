using Gtk;
using MotorControllerApp;
using System.Device.Gpio;


IGpioController gpioController = OperatingSystem.IsWindows() ?
    new FakeGpioController() :
    new GpioControllerWrapper(new GpioController());

using StepperMotorController ctrl = new StepperMotorController(gpioController, new ControllerConfig());

//await ctrl.HomeAsync();

await ctrl.MoveInchesAsync(1, 80);

await ctrl.StopAsync();



/*
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
*/

