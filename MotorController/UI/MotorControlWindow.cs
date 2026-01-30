using Gtk;
using Step;


namespace MotorControllerApp;

public class MotorControlWindow : ApplicationWindow
{
    private readonly StepperMotorController _motorController;
    private readonly DrawingArea _minLimitIndicator;
    private readonly DrawingArea _maxLimitIndicator;
    private readonly Label _speedLabel;
    private readonly Label _positionLabel;
    private readonly Button _startButton;
    private readonly Button _homeButton;
    private readonly Button _stopButton;
    private readonly Entry _rpmEntry;
    private readonly Entry _distanceEntry;
    
    private bool _isRunning = false;
    private uint _updateTimerId;

    public MotorControlWindow(StepperMotorController motorController) : base()
    {
        // Initialize motor controller
        _motorController = motorController;

        // Configure window
        SetTitle("Stepper Motor Controller");
        SetDefaultSize(800, 480);
        SetResizable(false);

        // Create main layout
        var mainBox = Box.New(Orientation.Vertical, 10);
        mainBox.SetMarginTop(20);
        mainBox.SetMarginBottom(20);
        mainBox.SetMarginStart(20);
        mainBox.SetMarginEnd(20);

        // Create limit switch indicators at top
        var limitSwitchBox = Box.New(Orientation.Horizontal, 20);
        limitSwitchBox.SetHalign(Align.Center);
        
        var minLimitBox = Box.New(Orientation.Vertical, 5);
        var minLimitLabel = Label.New("Min Limit");
        _minLimitIndicator = DrawingArea.New();
        _minLimitIndicator.SetSizeRequest(60, 60);
        _minLimitIndicator.SetDrawFunc(DrawMinLimitIndicator);
        minLimitBox.Append(minLimitLabel);
        minLimitBox.Append(_minLimitIndicator);
        
        var maxLimitBox = Box.New(Orientation.Vertical, 5);
        var maxLimitLabel = Label.New("Max Limit");
        _maxLimitIndicator = DrawingArea.New();
        _maxLimitIndicator.SetSizeRequest(60, 60);
        _maxLimitIndicator.SetDrawFunc(DrawMaxLimitIndicator);
        maxLimitBox.Append(maxLimitLabel);
        maxLimitBox.Append(_maxLimitIndicator);
        
        limitSwitchBox.Append(minLimitBox);
        limitSwitchBox.Append(maxLimitBox);
        mainBox.Append(limitSwitchBox);

        // Add separator
        mainBox.Append(Separator.New(Orientation.Horizontal));

        // Create status display boxes
        var statusBox = Box.New(Orientation.Horizontal, 20);
        statusBox.SetHalign(Align.Center);
        statusBox.SetMarginTop(20);
        statusBox.SetMarginBottom(20);

        // Speed display
        var speedFrame = Frame.New("Motor Speed (RPM)");
        var speedBox = Box.New(Orientation.Vertical, 5);
        speedBox.SetMarginTop(10);
        speedBox.SetMarginBottom(10);
        speedBox.SetMarginStart(20);
        speedBox.SetMarginEnd(20);
        _speedLabel = Label.New("0.0");
        _speedLabel.SetCssClasses(new[] { "large-text" });
        speedBox.Append(_speedLabel);
        speedFrame.SetChild(speedBox);
        speedFrame.SetSizeRequest(300, 100);

        // Position display
        var positionFrame = Frame.New("Current Position (inches)");
        var positionBox = Box.New(Orientation.Vertical, 5);
        positionBox.SetMarginTop(10);
        positionBox.SetMarginBottom(10);
        positionBox.SetMarginStart(20);
        positionBox.SetMarginEnd(20);
        _positionLabel = Label.New("0.000");
        _positionLabel.SetCssClasses(new[] { "large-text" });
        positionBox.Append(_positionLabel);
        positionFrame.SetChild(positionBox);
        positionFrame.SetSizeRequest(300, 100);

        statusBox.Append(speedFrame);
        statusBox.Append(positionFrame);
        mainBox.Append(statusBox);

        // Input controls
        var inputBox = Box.New(Orientation.Horizontal, 20);
        inputBox.SetHalign(Align.Center);
        inputBox.SetMarginTop(20);

        var rpmBox = Box.New(Orientation.Vertical, 5);
        var rpmLabel = Label.New("RPM:");
        _rpmEntry = Entry.New();
        _rpmEntry.SetText("60");
        _rpmEntry.SetSizeRequest(150, -1);
        rpmBox.Append(rpmLabel);
        rpmBox.Append(_rpmEntry);

        var distanceBox = Box.New(Orientation.Vertical, 5);
        var distanceLabel = Label.New("Distance (inches):");
        _distanceEntry = Entry.New();
        _distanceEntry.SetText("1.0");
        _distanceEntry.SetSizeRequest(150, -1);
        distanceBox.Append(distanceLabel);
        distanceBox.Append(_distanceEntry);

        inputBox.Append(rpmBox);
        inputBox.Append(distanceBox);
        mainBox.Append(inputBox);

        // Control buttons
        var buttonBox = Box.New(Orientation.Horizontal, 15);
        buttonBox.SetHalign(Align.Center);
        buttonBox.SetMarginTop(30);

        _startButton = Button.NewWithLabel("Start");
        _startButton.SetSizeRequest(150, 60);
        _startButton.OnClicked += OnStartClicked;

        _stopButton = Button.NewWithLabel("Stop");
        _stopButton.SetSizeRequest(150, 60);
        _stopButton.SetSensitive(false);
        _stopButton.OnClicked += OnStopClicked;

        _homeButton = Button.NewWithLabel("Home");
        _homeButton.SetSizeRequest(150, 60);
        _homeButton.OnClicked += OnHomeClicked;

        buttonBox.Append(_startButton);
        buttonBox.Append(_stopButton);
        buttonBox.Append(_homeButton);
        mainBox.Append(buttonBox);

        // Apply CSS styling
        ApplyCSS();

        SetChild(mainBox);

        // Start update timer
        _updateTimerId = GLib.Functions.TimeoutAdd(0, 100, UpdateDisplay);

        // Handle window close
        OnCloseRequest += (sender, args) =>
        {
            GLib.Functions.SourceRemove(_updateTimerId);
            _motorController.Dispose();
            return false;
        };
    }

    private void ApplyCSS()
    {
        var cssProvider = CssProvider.New();
        cssProvider.LoadFromData("""
            .large-text {
                font-size: 32px;
                font-weight: bold;
            }
            """);

        StyleContext.AddProviderForDisplay(
            Display.GetDefault()!,
            cssProvider,
            Gtk.StyleProvider.PRIORITY_APPLICATION
        );
    }

    private void DrawMinLimitIndicator(DrawingArea area, Cairo.Context context, int width, int height)
    {
        DrawCircleIndicator(context, width, height, _motorController.IsMinLimitSwitchActive);
    }

    private void DrawMaxLimitIndicator(DrawingArea area, Cairo.Context context, int width, int height)
    {
        DrawCircleIndicator(context, width, height, _motorController.IsMaxLimitSwitchActive);
    }

    private void DrawCircleIndicator(Cairo.Context context, int width, int height, bool isTriggered)
    {
        double centerX = width / 2.0;
        double centerY = height / 2.0;
        double radius = Math.Min(width, height) / 2.0 - 5;

        // Set color based on limit switch state
        if (isTriggered)
        {
            context.SetSourceRgb(1.0, 0.0, 0.0); // Red
        }
        else
        {
            context.SetSourceRgb(0.0, 0.8, 0.0); // Green
        }

        // Draw circle
        context.Arc(centerX, centerY, radius, 0, 2 * Math.PI);
        context.Fill();

        // Draw border
        context.SetSourceRgb(0.2, 0.2, 0.2);
        context.SetLineWidth(2);
        context.Arc(centerX, centerY, radius, 0, 2 * Math.PI);
        context.Stroke();
    }

    private bool UpdateDisplay()
    {
        _positionLabel.SetText($"{_motorController.CurrentPositionInches:F3}");
        
        // Queue redraw for limit indicators
        _minLimitIndicator.QueueDraw();
        _maxLimitIndicator.QueueDraw();

        return true; // Continue timer
    }

    private async void OnStartClicked(Button sender, EventArgs args)
    {
        if (_isRunning) return;

        try
        {
            if (!double.TryParse(_rpmEntry.GetText(), out double rpm) || rpm <= 0)
            {
                ShowErrorDialog("Invalid RPM value");
                return;
            }

            if (!double.TryParse(_distanceEntry.GetText(), out double distance))
            {
                ShowErrorDialog("Invalid distance value");
                return;
            }

            _isRunning = true;
            _startButton.SetSensitive(false);
            _stopButton.SetSensitive(true);
            _homeButton.SetSensitive(false);

            // Update speed display
            _speedLabel.SetText($"{rpm:F1}");

            await Task.Run(async () =>
            {
                try
                {
                    await _motorController.MoveInchesAsync(distance, rpm);
                }
                catch (Exception ex)
                {
                    await MainContext.Default().InvokeAsync(() =>
                    {
                        ShowErrorDialog($"Motor error: {ex.Message}");
                    });
                }
                finally
                {
                    await MainContext.Default().InvokeAsync(() =>
                    {
                        _isRunning = false;
                        _startButton.SetSensitive(true);
                        _stopButton.SetSensitive(false);
                        _homeButton.SetSensitive(true);
                        _speedLabel.SetText("0.0");
                    });
                }
            });
        }
        catch (Exception ex)
        {
            ShowErrorDialog($"Error: {ex.Message}");
            _isRunning = false;
            _startButton.SetSensitive(true);
            _stopButton.SetSensitive(false);
            _homeButton.SetSensitive(true);
        }
    }

    private async void OnStopClicked(Button sender, EventArgs args)
    {
        try
        {
            await _motorController.StopAsync();
            _stopButton.SetSensitive(false);
        }
        catch (Exception ex)
        {
            ShowErrorDialog($"Error stopping motor: {ex.Message}");
        }
    }

    private async void OnHomeClicked(Button sender, EventArgs args)
    {
        if (_isRunning) return;

        try
        {
            _isRunning = true;
            _startButton.SetSensitive(false);
            _homeButton.SetSensitive(false);
            _stopButton.SetSensitive(true);

            if (double.TryParse(_rpmEntry.GetText(), out double rpm) && rpm > 0)
            {
                _speedLabel.SetText($"{rpm:F1}");
                
                await Task.Run(async () =>
                {
                    try
                    {
                        await _motorController.HomeAsync(rpm);
                    }
                    catch (Exception ex)
                    {
                        await MainContext.Default().InvokeAsync(() =>
                        {
                            ShowErrorDialog($"Homing error: {ex.Message}");
                        });
                    }
                    finally
                    {
                        await MainContext.Default().InvokeAsync(() =>
                        {
                            _isRunning = false;
                            _startButton.SetSensitive(true);
                            _stopButton.SetSensitive(false);
                            _homeButton.SetSensitive(true);
                            _speedLabel.SetText("0.0");
                        });
                    }
                });
            }
            else
            {
                await _motorController.HomeAsync();
                _isRunning = false;
                _startButton.SetSensitive(true);
                _stopButton.SetSensitive(false);
                _homeButton.SetSensitive(true);
                _speedLabel.SetText("0.0");
            }
        }
        catch (Exception ex)
        {
            ShowErrorDialog($"Error homing motor: {ex.Message}");
            _isRunning = false;
            _startButton.SetSensitive(true);
            _stopButton.SetSensitive(false);
            _homeButton.SetSensitive(true);
        }
    }

    private void ShowErrorDialog(string message)
    {
        var dialog = MessageDialog.New(
            this,
            DialogFlags.Modal,
            MessageType.Error,
            ButtonsType.Ok,
            message
        );
        dialog.OnResponse += (sender, args) => dialog.Destroy();
        dialog.Show();
    }
}