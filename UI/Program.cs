using Gtk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MotorControllerApp;
using System.Device.Gpio;
using UI;


var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((ctx, services) =>
{
    // Initialize GPIO controller
    if (!OperatingSystem.IsWindows())
    {
        services.AddSingleton<GpioController>();
        services.AddSingleton<IGpioController, GpioControllerWrapper>();
    }
    else
        services.AddSingleton<IGpioController, FakeGpioController>();

    services.Configure<ControllerConfig>(ctx.Configuration.GetSection("ControllerConfig"));
    services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ControllerConfig>>().Value);

    services.AddSingleton<IStepperMotorController, StepperMotorController>();

}).ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
}).ConfigureAppConfiguration((ctx, config) =>
{
    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
});


// ============================
// Build the host
// ============================
using var host = builder.Build();

using var motorController = host.Services.GetRequiredService<IStepperMotorController>();
var config = host.Services.GetRequiredService<ControllerConfig>();

// ============================
// Create and run application
// ============================
var application = Application.New("motorcontroller.app", Gio.ApplicationFlags.FlagsNone);

application.OnActivate += (sender, args) =>
{
    CreateMainWindow((Application)sender, motorController, config);
};

return application.RunWithSynchronizationContext(null);


// Create main window
static void CreateMainWindow(Application app, IStepperMotorController motorController, ControllerConfig config)
{
    var window = ApplicationWindow.New(app);
    window.SetDecorated(true);
    window.SetDefaultSize(800, 480);
    window.SetResizable(false);

    var ui = new MotorControlUI(window, motorController, config);
    window.Show();
}
