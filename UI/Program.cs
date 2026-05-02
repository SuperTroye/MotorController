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
    if (IsRaspberryPi())
    {
        services.AddSingleton<GpioController>();
        services.AddSingleton<IGpioController, GpioControllerWrapper>();
    }
    else // Windows, Linux dev machines, macOS — use simulation
        services.AddSingleton<IGpioController, FakeGpioController>();

    services.Configure<LinearAxisConfig>(ctx.Configuration.GetSection("LinearAxisConfig"));
    services.Configure<RotaryAxisConfig>(ctx.Configuration.GetSection("RotaryAxisConfig"));
    services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LinearAxisConfig>>().Value);
    services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RotaryAxisConfig>>().Value);

    services.AddSingleton(sp => new SynchronizedDualAxisConfig 
    { 
        LinearAxisConfig = sp.GetRequiredService<LinearAxisConfig>(), 
        RotaryAxisConfig = sp.GetRequiredService<RotaryAxisConfig>(),
        GearRatio = 0.4
    });

    services.AddSingleton<ISynchronizedDualAxisController, SynchronizedDualAxisController>();

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

using var motorController = host.Services.GetRequiredService<ISynchronizedDualAxisController>();
var config = host.Services.GetRequiredService<SynchronizedDualAxisConfig>();

// ============================
// Create and run application
// ============================
var application = Application.New("motorcontroller.app", Gio.ApplicationFlags.FlagsNone);

var cssProvider = Gtk.CssProvider.New();
cssProvider.LoadFromPath("./Styles/style.css");

Gtk.StyleContext.AddProviderForDisplay(
    Gdk.Display.GetDefault()!, cssProvider, 600 // GTK_STYLE_PROVIDER_PRIORITY_APPLICATION
);      

application.OnActivate += (sender, args) =>
{
    CreateMainWindow((Application)sender, motorController, config);
};

return application.RunWithSynchronizationContext(null);


// Detect Raspberry Pi hardware
static bool IsRaspberryPi()
{
    try
    {
        return File.ReadAllText("/proc/device-tree/model")
                   .Contains("Raspberry Pi", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        return false;
    }
}

// Create main window
static void CreateMainWindow(Application app, ISynchronizedDualAxisController motorController, SynchronizedDualAxisConfig config)
{
    var window = ApplicationWindow.New(app);
    window.SetDecorated(true);
    window.SetDefaultSize(800, 440);
    window.SetResizable(false);

    var ui = new MotorControlUI(window, motorController, config);
    window.Show();
}
