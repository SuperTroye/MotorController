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

    // Create main container
    var mainBox = Box.New(Orientation.Vertical, 10);
    mainBox.SetMarginStart(20);
    mainBox.SetMarginEnd(20);
    mainBox.SetMarginTop(20);
    mainBox.SetMarginBottom(20);

    // Title label
    var titleLabel = Label.New("Motor Controller");
    titleLabel.AddCssClass("title-1");
    mainBox.Append(titleLabel);

    // Status indicators container
    var statusBox = Box.New(Orientation.Horizontal, 20);
    statusBox.SetHalign(Align.Center);
    statusBox.SetMarginTop(10);

    // Min limit switch indicator
    var minLimitBox = Box.New(Orientation.Vertical, 5);
    var minLimitLabel = Label.New("Min Limit");
    var minLimitIndicator = DrawingArea.New();
    minLimitIndicator.SetContentWidth(40);
    minLimitIndicator.SetContentHeight(40);
    minLimitIndicator.SetDrawFunc((area, context, width, height) =>
    {
        bool triggered = stepperMotor.IsMinLimitSwitchTriggered;
        context.SetSourceRgb(triggered ? 1.0 : 0.0, triggered ? 0.0 : 1.0, 0.0);
        context.Arc(width / 2.0, height / 2.0, 15, 0, 2 * Math.PI);
        context.Fill();
    });
    minLimitBox.Append(minLimitLabel);
    minLimitBox.Append(minLimitIndicator);
    statusBox.Append(minLimitBox);

    // Max limit switch indicator
    var maxLimitBox = Box.New(Orientation.Vertical, 5);
    var maxLimitLabel = Label.New("Max Limit");
    var maxLimitIndicator = DrawingArea.New();
    maxLimitIndicator.SetContentWidth(40);
    maxLimitIndicator.SetContentHeight(40);
    maxLimitIndicator.SetDrawFunc((area, context, width, height) =>
    {
        bool triggered = stepperMotor.IsMaxLimitSwitchTriggered;
        context.SetSourceRgb(triggered ? 1.0 : 0.0, triggered ? 0.0 : 1.0, 0.0);
        context.Arc(width / 2.0, height / 2.0, 15, 0, 2 * Math.PI);
        context.Fill();
    });
    maxLimitBox.Append(maxLimitLabel);
    maxLimitBox.Append(maxLimitIndicator);
    statusBox.Append(maxLimitBox);

    mainBox.Append(statusBox);

    // Update indicators periodically
    GLib.Functions.TimeoutAdd(0, 100, () =>
    {
        minLimitIndicator.QueueDraw();
        maxLimitIndicator.QueueDraw();
        return true;
    });

    // Motor information labels
    var infoBox = Box.New(Orientation.Vertical, 10);
    infoBox.SetHalign(Align.Center);
    infoBox.SetMarginTop(20);

    var speedLabel = Label.New("Speed: 0 RPM");
    speedLabel.AddCssClass("title-3");
    var positionLabel = Label.New("Position: 0.00 inches");
    positionLabel.AddCssClass("title-3");

    infoBox.Append(speedLabel);
    infoBox.Append(positionLabel);
    mainBox.Append(infoBox);

    // Update position label periodically
    GLib.Functions.TimeoutAdd(0, 100, () =>
    {
        positionLabel.SetText($"Position: {stepperMotor.CurrentPositionInches:F2} inches");
        return true;
    });

    // Input fields container
    var inputBox = Box.New(Orientation.Horizontal, 20);
    inputBox.SetHalign(Align.Center);
    inputBox.SetMarginTop(20);

    // Speed input
    var speedInputBox = Box.New(Orientation.Vertical, 5);
    var speedInputLabel = Label.New("Speed (RPM)");
    var speedEntry = Entry.New();
    speedEntry.SetText("60");
    speedEntry.SetMaxWidthChars(10);
    speedEntry.SetAlignment(0.5f);
    speedInputBox.Append(speedInputLabel);
    speedInputBox.Append(speedEntry);
    inputBox.Append(speedInputBox);

    // Position input
    var positionInputBox = Box.New(Orientation.Vertical, 5);
    var positionInputLabel = Label.New("Position (inches)");
    var positionEntry = Entry.New();
    positionEntry.SetText("0.00");
    positionEntry.SetMaxWidthChars(10);
    positionEntry.SetAlignment(0.5f);
    positionInputBox.Append(positionInputLabel);
    positionInputBox.Append(positionEntry);
    inputBox.Append(positionInputBox);

    // Move button
    var moveButton = Button.NewWithLabel("MOVE TO POSITION");
    moveButton.SetSizeRequest(150, 50);
    moveButton.OnClicked += async (sender, args) =>
    {
        try
        {
            // Validate speed input
            if (!double.TryParse(speedEntry.GetText(), out double rpm) || rpm <= 0 || rpm > 500)
            {
                ShowErrorDialog(window, "Invalid speed. Please enter a value between 0 and 500 RPM.");
                return;
            }

            // Validate position input
            if (!double.TryParse(positionEntry.GetText(), out double targetPosition) || targetPosition < 0 || targetPosition > 12)
            {
                ShowErrorDialog(window, "Invalid position. Please enter a value between 0 and 12 inches.");
                return;
            }

            // Calculate distance to move
            double currentPosition = stepperMotor.CurrentPositionInches;
            double distance = targetPosition - currentPosition;

            if (Math.Abs(distance) < 0.01)
            {
                ShowInfoDialog(window, "Already at target position.");
                return;
            }

            // Move to position
            speedLabel.SetText($"Speed: {rpm:F0} RPM");
            await stepperMotor.MoveInchesAsync(distance, rpm);
            speedLabel.SetText("Speed: 0 RPM");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Error moving to position: {ex.Message}");
            speedLabel.SetText("Speed: 0 RPM");
            ShowWarningDialog(window, $"Movement stopped: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error moving to position: {ex.Message}");
            speedLabel.SetText("Speed: 0 RPM");
            ShowErrorDialog(window, $"Error: {ex.Message}");
        }
    };
    inputBox.Append(moveButton);

    mainBox.Append(inputBox);

    // Control buttons container
    var controlBox = Box.New(Orientation.Horizontal, 10);
    controlBox.SetHalign(Align.Center);
    controlBox.SetMarginTop(20);

    // Run to min limit button
    var minButton = Button.New();
    minButton.SetIconName("go-previous-symbolic");
    minButton.SetSizeRequest(80, 80);
    minButton.SetTooltipText("Run to Min Limit");
    minButton.OnClicked += async (sender, args) =>
    {
        try
        {
            // Get speed from input
            if (!double.TryParse(speedEntry.GetText(), out double rpm) || rpm <= 0 || rpm > 200)
            {
                rpm = 60; // Default speed
            }
            
            speedLabel.SetText($"Speed: {rpm:F0} RPM");
            await stepperMotor.RunToLimitSwitchAsync(rpm, MotorDirection.Counterclockwise);
            speedLabel.SetText("Speed: 0 RPM");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running to min limit: {ex.Message}");
            speedLabel.SetText("Speed: 0 RPM");
        }
    };
    controlBox.Append(minButton);

    // Home button
    var homeButton = Button.NewWithLabel("HOME");
    homeButton.SetSizeRequest(100, 80);
    homeButton.OnClicked += async (sender, args) =>
    {
        try
        {
            // Get speed from input
            if (!double.TryParse(speedEntry.GetText(), out double rpm) || rpm <= 0 || rpm > 200)
            {
                rpm = 60; // Default speed
            }
            
            speedLabel.SetText($"Speed: {rpm:F0} RPM");
            await stepperMotor.HomeAsync(rpm);
            speedLabel.SetText("Speed: 0 RPM");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error homing motor: {ex.Message}");
            speedLabel.SetText("Speed: 0 RPM");
        }
    };
    controlBox.Append(homeButton);

    // Run to max limit button
    var maxButton = Button.New();
    maxButton.SetIconName("go-next-symbolic");
    maxButton.SetSizeRequest(80, 80);
    maxButton.SetTooltipText("Run to Max Limit");
    maxButton.OnClicked += async (sender, args) =>
    {
        try
        {
            // Get speed from input
            if (!double.TryParse(speedEntry.GetText(), out double rpm) || rpm <= 0 || rpm > 200)
            {
                rpm = 60; // Default speed
            }
            
            speedLabel.SetText($"Speed: {rpm:F0} RPM");
            await stepperMotor.RunToLimitSwitchAsync(rpm, MotorDirection.Clockwise);
            speedLabel.SetText("Speed: 0 RPM");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running to max limit: {ex.Message}");
            speedLabel.SetText("Speed: 0 RPM");
        }
    };
    controlBox.Append(maxButton);

    mainBox.Append(controlBox);

    // Set the main box as window child
    window.SetChild(mainBox);
    window.Show();
};

return application.RunWithSynchronizationContext(null);

// Helper methods for dialogs
static void ShowErrorDialog(Gtk.Window parent, string message)
{
    var dialog = new Gtk.AlertDialog();
    dialog.SetMessage( message);
    dialog.Show(parent);
}

static void ShowWarningDialog(Gtk.Window parent, string message)
{
    var dialog = new Gtk.AlertDialog();
    dialog.SetMessage(message);
    dialog.Show(parent);
}

static void ShowInfoDialog(Gtk.Window parent, string message)
{
    var dialog = new Gtk.AlertDialog();
    dialog.SetMessage(message);
    dialog.Show(parent);
}
