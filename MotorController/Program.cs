using Gtk;
using MotorControllerApp;
using System.Device.Gpio;


IGpioController gpioController = new GpioControllerWrapper(new GpioController());
//IGpioController gpioController = new FakeGpioController();

StepperMotorController stepperMotor = new StepperMotorController(gpioController, new StepperMotorSettings());

var application = Application.New("com.motorcontroller.app", Gio.ApplicationFlags.FlagsNone);
application.OnActivate += (sender, args) =>
{
    var window = Gtk.ApplicationWindow.New((Gtk.Application)sender);
    window.Title = "Motor Control";
    window.SetDefaultSize(800, 480);
    window.Resizable = false;

    // Main vertical box container
    var mainBox = Box.New(Orientation.Vertical, 10);
    mainBox.SetMarginTop(20);
    mainBox.SetMarginBottom(20);
    mainBox.SetMarginStart(20);
    mainBox.SetMarginEnd(20);

    // Top section: Limit switch indicators
    var limitSwitchBox = Box.New(Orientation.Horizontal, 30);
    limitSwitchBox.SetHalign(Align.Center);
    
    // Min limit switch indicator
    var minLimitBox = Box.New(Orientation.Vertical, 5);
    var minLimitLabel = Label.New("Min Limit");
    var minLimitCircle = DrawingArea.New();
    minLimitCircle.SetSizeRequest(60, 60);
    minLimitCircle.SetDrawFunc((area, context, width, height) =>
    {
        // Draw circle
        context.Arc(width / 2.0, height / 2.0, 25, 0, 2 * Math.PI);
        context.SetSourceRgb(0, 1, 0); // Green - no limit detected
        context.Fill();
    });
    minLimitBox.Append(minLimitLabel);
    minLimitBox.Append(minLimitCircle);
    
    // Max limit switch indicator
    var maxLimitBox = Box.New(Orientation.Vertical, 5);
    var maxLimitLabel = Label.New("Max Limit");
    var maxLimitCircle = DrawingArea.New();
    maxLimitCircle.SetSizeRequest(60, 60);
    maxLimitCircle.SetDrawFunc((area, context, width, height) =>
    {
        // Draw circle
        context.Arc(width / 2.0, height / 2.0, 25, 0, 2 * Math.PI);
        context.SetSourceRgb(0, 1, 0); // Green - no limit detected
        context.Fill();
    });
    maxLimitBox.Append(maxLimitLabel);
    maxLimitBox.Append(maxLimitCircle);
    
    limitSwitchBox.Append(minLimitBox);
    limitSwitchBox.Append(maxLimitBox);
    mainBox.Append(limitSwitchBox);

    // Status display boxes
    var statusBox = Box.New(Orientation.Horizontal, 20);
    statusBox.SetHalign(Align.Center);
    statusBox.SetMarginTop(20);
    
    // Motor speed box
    var speedBoxContainer = Box.New(Orientation.Vertical, 5);
    var speedTitleLabel = Label.New("Motor Speed");
    speedTitleLabel.AddCssClass("title-4");
    var speedFrame = Frame.New(null);
    var speedLabel = Label.New("0 RPM");
    speedLabel.SetSizeRequest(180, 60);
    speedLabel.AddCssClass("title-1");
    speedFrame.SetChild(speedLabel);
    speedBoxContainer.Append(speedTitleLabel);
    speedBoxContainer.Append(speedFrame);
    
    // Current position box
    var positionBoxContainer = Box.New(Orientation.Vertical, 5);
    var positionTitleLabel = Label.New("Current Position");
    positionTitleLabel.AddCssClass("title-4");
    var positionFrame = Frame.New(null);
    var positionLabel = Label.New("0.000 in");
    positionLabel.SetSizeRequest(180, 60);
    positionLabel.AddCssClass("title-1");
    positionFrame.SetChild(positionLabel);
    positionBoxContainer.Append(positionTitleLabel);
    positionBoxContainer.Append(positionFrame);
    
    statusBox.Append(speedBoxContainer);
    statusBox.Append(positionBoxContainer);
    mainBox.Append(statusBox);

    // Control buttons
    var buttonBox = Box.New(Orientation.Horizontal, 20);
    buttonBox.SetHalign(Align.Center);
    buttonBox.SetMarginTop(30);
    
    var startButton = Button.NewWithLabel("Start Motor");
    startButton.SetSizeRequest(180, 60);
    startButton.AddCssClass("suggested-action");
    startButton.OnClicked += (sender, args) =>
    {
        // TODO: Start motor logic
        speedLabel.SetText("100 RPM");
    };
    
    var homeButton = Button.NewWithLabel("Home Motor");
    homeButton.SetSizeRequest(180, 60);
    homeButton.OnClicked += async (sender, args) =>
    {
        // TODO: Home motor logic
        positionLabel.SetText("0.000 in");
        await stepperMotor.HomeAsync();
    };
    
    buttonBox.Append(startButton);
    buttonBox.Append(homeButton);
    mainBox.Append(buttonBox);

    // Timer to update UI with motor status
    GLib.Functions.TimeoutAdd(0, 100, () =>
    {
        // Update position
        double position = stepperMotor.CurrentPositionInches;
        positionLabel.SetText($"{position:F3} in");
        
        // Update limit switches (you'll need to add properties to StepperMotorController)
        // For now, using placeholder logic
        
        // Update min limit circle
        minLimitCircle.SetDrawFunc((area, context, width, height) =>
        {
            context.Arc(width / 2.0, height / 2.0, 25, 0, 2 * Math.PI);
            // TODO: Check actual limit switch state
            context.SetSourceRgb(0, 1, 0); // Green
            context.Fill();
        });
        minLimitCircle.QueueDraw();
        
        // Update max limit circle
        maxLimitCircle.SetDrawFunc((area, context, width, height) =>
        {
            context.Arc(width / 2.0, height / 2.0, 25, 0, 2 * Math.PI);
            // TODO: Check actual limit switch state
            context.SetSourceRgb(0, 1, 0); // Green
            context.Fill();
        });
        maxLimitCircle.QueueDraw();
        
        return true; // Continue timer
    });

    window.SetChild(mainBox);
    window.Show();
};

return application.RunWithSynchronizationContext(null);
