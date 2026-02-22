using Gtk;
using MotorControllerApp;
using System.Diagnostics;
using Widgets;

namespace UI;

// UI controller class
public class MotorControlUI
{
    private readonly ApplicationWindow _window;
    private readonly IStepperMotorController _motorController;
    private readonly ControllerConfig _config;
    private readonly Label _speedLabel;
    private readonly Label _positionLabel;
    private readonly Entry _speedEntry;
    private readonly Entry _positionEntry;
    private readonly Button _moveButton;
    private readonly Button _stopButton;
    private readonly Button _minLimitButton;
    private readonly Button _maxLimitButton;
    private readonly Button _powerButton;
    private readonly Button _settingsButton;
    private readonly DrawingArea _minLimitIndicator;
    private readonly DrawingArea _maxLimitIndicator;
    private CancellationTokenSource? _moveCts;
    private const double MIN_RPM = 0;
    private const double MAX_RPM = 350;
    private double _currentRpm = 100;

    public MotorControlUI(ApplicationWindow window, IStepperMotorController motorController, ControllerConfig config)
    {
        _window = window;
        _motorController = motorController;
        _config = config;

        // Subscribe to limit switch events
        _motorController.MinLimitSwitchTriggered += OnMinLimitSwitchChanged;
        _motorController.MaxLimitSwitchTriggered += OnMaxLimitSwitchChanged;

        // Main container
        var mainBox = Box.New(Orientation.Vertical, 10);
        mainBox.SetMarginTop(20);
        mainBox.SetMarginBottom(20);
        mainBox.SetMarginStart(20);
        mainBox.SetMarginEnd(20);

        // Status section
        var statusBox = Box.New(Orientation.Horizontal, 20);
        statusBox.SetHexpand(true);
        statusBox.SetHalign(Align.Center);

        // Current speed display
        var speedBox = Box.New(Orientation.Vertical, 5);
        var speedTitleLabel = Label.New("Current Speed");
        _speedLabel = Label.New("0 RPM");
        speedBox.Append(speedTitleLabel);
        speedBox.Append(_speedLabel);

        // Current position display
        var positionBox = Box.New(Orientation.Vertical, 5);
        var positionTitleLabel = Label.New("Current Position");
        _positionLabel = Label.New("0.00 in");
        positionBox.Append(positionTitleLabel);
        positionBox.Append(_positionLabel);

        statusBox.Append(speedBox);
        statusBox.Append(positionBox);
        mainBox.Append(statusBox);

        // Limit switch indicators
        var limitSwitchBox = Box.New(Orientation.Horizontal, 20);
        limitSwitchBox.SetHexpand(true);
        limitSwitchBox.SetHalign(Align.Center);
        limitSwitchBox.SetMarginTop(10);

        // Min limit indicator
        var minLimitBox = Box.New(Orientation.Horizontal, 10);
        var minLimitLabel = Label.New("Min Limit");
        _minLimitIndicator = DrawingArea.New();
        _minLimitIndicator.SetSizeRequest(30, 30);
        _minLimitIndicator.SetDrawFunc(DrawMinLimitIndicator);
        minLimitBox.Append(minLimitLabel);
        minLimitBox.Append(_minLimitIndicator);

        // Max limit indicator
        var maxLimitBox = Box.New(Orientation.Horizontal, 10);
        var maxLimitLabel = Label.New("Max Limit");
        _maxLimitIndicator = DrawingArea.New();
        _maxLimitIndicator.SetSizeRequest(30, 30);
        _maxLimitIndicator.SetDrawFunc(DrawMaxLimitIndicator);
        maxLimitBox.Append(maxLimitLabel);
        maxLimitBox.Append(_maxLimitIndicator);

        limitSwitchBox.Append(minLimitBox);
        limitSwitchBox.Append(maxLimitBox);
        mainBox.Append(limitSwitchBox);

        // Control buttons for limit switches
        var limitButtonBox = Box.New(Orientation.Horizontal, 10);
        limitButtonBox.SetHexpand(true);
        limitButtonBox.SetHalign(Align.Center);
        limitButtonBox.SetMarginTop(20);

        _minLimitButton = Button.NewWithLabel("◀ Min Limit");
        _minLimitButton.SetSizeRequest(150, 50);
        _minLimitButton.OnClicked += OnMinLimitButtonClicked;

        _maxLimitButton = Button.NewWithLabel("Max Limit ▶");
        _maxLimitButton.SetSizeRequest(150, 50);
        _maxLimitButton.OnClicked += OnMaxLimitButtonClicked;

        limitButtonBox.Append(_minLimitButton);
        limitButtonBox.Append(_maxLimitButton);
        mainBox.Append(limitButtonBox);

        // Speed input
        var speedInputBox = Box.New(Orientation.Horizontal, 10);
        speedInputBox.SetHexpand(true);
        speedInputBox.SetHalign(Align.Center);
        speedInputBox.SetMarginTop(20);

        var speedInputLabel = Label.New("Speed (RPM):");
        speedInputLabel.SetSizeRequest(120, -1);
        _speedEntry = Entry.New();
        _speedEntry.SetText("100");
        _speedEntry.SetSizeRequest(80, -1);
        _speedEntry.SetEditable(false);
        _speedEntry.SetCanFocus(true);

        // Add click handler to show keypad
        var speedEntryClick = GestureClick.New();
        speedEntryClick.SetPropagationPhase(Gtk.PropagationPhase.Capture);
        speedEntryClick.OnPressed += (sender, args) => ShowKeypad(_speedEntry, "Speed (RPM)", MIN_RPM, MAX_RPM);
        _speedEntry.AddController(speedEntryClick);
        _speedEntry.OnChanged += (sender, args) => OnSpeedEntryChanged();

        speedInputBox.Append(speedInputLabel);
        speedInputBox.Append(_speedEntry);
        mainBox.Append(speedInputBox);

        // Position input
        var positionInputBox = Box.New(Orientation.Horizontal, 10);
        positionInputBox.SetHexpand(true);
        positionInputBox.SetHalign(Align.Center);

        var positionInputLabel = Label.New("Position (in):");
        positionInputLabel.SetSizeRequest(120, -1);
        _positionEntry = Entry.New();
        _positionEntry.SetText("0");
        _positionEntry.SetSizeRequest(80, -1);
        _positionEntry.SetEditable(false);
        _positionEntry.SetCanFocus(true);

        // Add click handler to show keypad
        var positionEntryClick = GestureClick.New();
        positionEntryClick.SetPropagationPhase(Gtk.PropagationPhase.Capture);
        positionEntryClick.OnPressed += (sender, args) => ShowKeypad(_positionEntry, "Position (inches)", null, null);
        _positionEntry.AddController(positionEntryClick);

        positionInputBox.Append(positionInputLabel);
        positionInputBox.Append(_positionEntry);
        mainBox.Append(positionInputBox);

        // Move and Stop buttons
        var actionButtonBox = Box.New(Orientation.Horizontal, 10);
        actionButtonBox.SetHexpand(true);
        actionButtonBox.SetHalign(Align.Center);
        actionButtonBox.SetMarginTop(20);

        _moveButton = Button.NewWithLabel("Move to Position");
        _moveButton.SetSizeRequest(180, 50);
        _moveButton.OnClicked += OnMoveButtonClicked;

        _stopButton = Button.NewWithLabel("⬛ STOP");
        _stopButton.SetSizeRequest(150, 50);
        _stopButton.OnClicked += OnStopButtonClicked;

        actionButtonBox.Append(_moveButton);
        actionButtonBox.Append(_stopButton);
        mainBox.Append(actionButtonBox);

        // Settings and Power buttons
        var bottomButtonBox = Box.New(Orientation.Horizontal, 10);
        bottomButtonBox.SetHexpand(true);
        bottomButtonBox.SetHalign(Align.Fill);
        bottomButtonBox.SetMarginTop(20);

        _settingsButton = Button.NewWithLabel("⚙ Settings");
        _settingsButton.SetSizeRequest(120, 40);
        _settingsButton.SetHalign(Align.Start);
        _settingsButton.OnClicked += OnSettingsButtonClicked;

        _powerButton = Button.NewWithLabel("⏻ Power");
        _powerButton.SetSizeRequest(120, 40);
        _powerButton.SetHalign(Align.End);
        _powerButton.OnClicked += OnPowerButtonClicked;

        bottomButtonBox.Append(_settingsButton);
        bottomButtonBox.Append(_powerButton);
        mainBox.Append(bottomButtonBox);

        _window.SetChild(mainBox);

        // Start position update timer
        GLib.Functions.TimeoutAdd(0, 100, UpdateDisplay);
    }

    private void DrawMinLimitIndicator(DrawingArea area, Cairo.Context cr, int width, int height)
    {
        var radius = Math.Min(width, height) / 2.0;
        cr.Arc(width / 2.0, height / 2.0, radius * 0.8, 0, 2 * Math.PI);

        if (_motorController.IsMinLimitSwitchTriggered)
        {
            cr.SetSourceRgb(1.0, 0.0, 0.0); // Red
        }
        else
        {
            cr.SetSourceRgb(0.0, 1.0, 0.0); // Green
        }

        cr.Fill();
    }

    private void DrawMaxLimitIndicator(DrawingArea area, Cairo.Context cr, int width, int height)
    {
        var radius = Math.Min(width, height) / 2.0;
        cr.Arc(width / 2.0, height / 2.0, radius * 0.8, 0, 2 * Math.PI);

        if (_motorController.IsMaxLimitSwitchTriggered)
        {
            cr.SetSourceRgb(1.0, 0.0, 0.0); // Red
        }
        else
        {
            cr.SetSourceRgb(0.0, 1.0, 0.0); // Green
        }

        cr.Fill();
    }

    private void OnMinLimitSwitchChanged(object? sender, EventArgs e)
    {
        // Update indicator on GTK main thread
        GLib.Functions.IdleAdd(0, () =>
        {
            _minLimitIndicator.QueueDraw();
            return false;
        });
    }

    private void OnMaxLimitSwitchChanged(object? sender, EventArgs e)
    {
        // Update indicator on GTK main thread
        GLib.Functions.IdleAdd(0, () =>
        {
            _maxLimitIndicator.QueueDraw();
            return false;
        });
    }

    private bool UpdateDisplay()
    {
        _positionLabel.SetText($"{_motorController.CurrentPositionInches:F2} in");
        // Speed display would be updated during actual motion
        return true;
    }

    private void OnSpeedEntryChanged()
    {
        var text = _speedEntry.GetText();
        if (double.TryParse(text, out var rpm) && rpm >= MIN_RPM && rpm <= MAX_RPM)
        {
            _currentRpm = rpm;
        }
    }

    private async void OnMinLimitButtonClicked(Button sender, EventArgs args)
    {
        try
        {
            _moveCts?.Cancel();
            _moveCts = new CancellationTokenSource();

            SetButtonsEnabled(false);
            await _motorController.RunToLimitSwitchAsync(false, _currentRpm, _moveCts.Token);
            await _motorController.ResetPositionAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Error moving to min limit: {ex.Message}");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void OnMaxLimitButtonClicked(Button sender, EventArgs args)
    {
        try
        {
            _moveCts?.Cancel();
            _moveCts = new CancellationTokenSource();

            SetButtonsEnabled(false);
            await _motorController.RunToLimitSwitchAsync(true, _currentRpm, _moveCts.Token);
        }
        catch (Exception ex)
        {
            ShowError($"Error moving to max limit: {ex.Message}");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void OnMoveButtonClicked(Button sender, EventArgs args)
    {
        var text = _positionEntry.GetText();
        if (!double.TryParse(text, out var targetPosition))
        {
            ShowError("Invalid position value. Please enter a valid number.");
            return;
        }

        var currentPosition = _motorController.CurrentPositionInches;
        var distance = targetPosition - currentPosition;

        try
        {
            _moveCts?.Cancel();
            _moveCts = new CancellationTokenSource();

            SetButtonsEnabled(false);
            _speedLabel.SetText($"{_currentRpm:F0} RPM");
            await _motorController.MoveInchesAsync(distance, _currentRpm, _moveCts.Token);
            _speedLabel.SetText("0 RPM");
        }
        catch (Exception ex)
        {
            ShowError($"Error moving to position: {ex.Message}");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void OnStopButtonClicked(Button sender, EventArgs args)
    {
        try
        {
            _moveCts?.Cancel();
            await _motorController.StopAsync();
            _speedLabel.SetText("0 RPM");
        }
        catch (Exception ex)
        {
            ShowError($"Error stopping motor: {ex.Message}");
        }
    }

    private void OnPowerButtonClicked(Button sender, EventArgs args)
    {
        var dialog = new Dialog();
        dialog.SetTitle("System Power");
        dialog.SetTransientFor(_window);
        dialog.SetModal(true);
        dialog.SetDefaultSize(300, 200);

        var contentBox = Box.New(Orientation.Vertical, 10);
        contentBox.SetMarginTop(20);
        contentBox.SetMarginBottom(20);
        contentBox.SetMarginStart(20);
        contentBox.SetMarginEnd(20);

        var messageLabel = Label.New("Select an option:");
        contentBox.Append(messageLabel);

        var closeAppButton = Button.NewWithLabel("Close App");
        closeAppButton.SetSizeRequest(-1, 50);
        closeAppButton.OnClicked += (s, e) =>
        {
            dialog.Close();
            _window.Close();
        };
        contentBox.Append(closeAppButton);

        var shutdownButton = Button.NewWithLabel("Shutdown");
        shutdownButton.SetSizeRequest(-1, 50);
        shutdownButton.OnClicked += (s, e) =>
        {
            dialog.Close();
            ExecuteSystemCommand("shutdown", "/s /t 0");
        };
        contentBox.Append(shutdownButton);

        var restartButton = Button.NewWithLabel("Restart");
        restartButton.SetSizeRequest(-1, 50);
        restartButton.OnClicked += (s, e) =>
        {
            dialog.Close();
            ExecuteSystemCommand("shutdown", "/r /t 0");
        };
        contentBox.Append(restartButton);

        var cancelButton = Button.NewWithLabel("Cancel");
        cancelButton.SetSizeRequest(-1, 50);
        cancelButton.OnClicked += (s, e) => dialog.Close();
        contentBox.Append(cancelButton);

        dialog.SetChild(contentBox);
        dialog.Show();
    }

    private void ExecuteSystemCommand(string command, string arguments)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                // On Linux/Raspberry Pi, use appropriate commands
                var cmd = command == "shutdown" && arguments.Contains("/r") ? "reboot" : "shutdown";
                var args = cmd == "reboot" ? "" : "now";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "sudo",
                    Arguments = $"{cmd} {args}",
                    UseShellExecute = false
                });
            }
            else if (OperatingSystem.IsWindows())
            {
                // On Windows, use shutdown command
                Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false
                });
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error executing system command: {ex.Message}");
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _moveButton.SetSensitive(enabled);
        _minLimitButton.SetSensitive(enabled);
        _maxLimitButton.SetSensitive(enabled);
    }

    private void ShowKeypad(Entry targetEntry, string title, double? minValue, double? maxValue)
    {
        var dialog = new Dialog();
        dialog.SetTitle(title);
        dialog.SetTransientFor(_window);
        dialog.SetModal(true);
        dialog.SetDefaultSize(300, 300);

        var keypad = new Keypad();

        // Set initial value
        var currentValue = targetEntry.GetText();
        if (!string.IsNullOrEmpty(currentValue))
        {
            keypad.Entry.SetText(currentValue);
        }

        // Handle close/confirm
        keypad.CloseRequested += (s, e) =>
        {
            var inputValue = keypad.Entry.GetText();

            // Validate numeric input
            if (!string.IsNullOrWhiteSpace(inputValue) && double.TryParse(inputValue, out var value))
            {
                // Check range if specified
                if (minValue.HasValue && value < minValue.Value)
                {
                    ShowError($"Value must be at least {minValue.Value}");
                    return;
                }
                if (maxValue.HasValue && value > maxValue.Value)
                {
                    ShowError($"Value must be no more than {maxValue.Value}");
                    return;
                }

                targetEntry.SetText(inputValue);
                dialog.Close();
            }
            else if (string.IsNullOrWhiteSpace(inputValue))
            {
                // Allow clearing the value
                targetEntry.SetText("0");
                dialog.Close();
            }
            else
            {
                ShowError("Please enter a valid number");
            }
        };

        dialog.SetChild(keypad);
        dialog.Show();
    }

    private void ShowError(string message)
    {
        var dialog = new Dialog();
        dialog.SetTitle("Error");
        dialog.SetTransientFor(_window);
        dialog.SetModal(true);

        var contentBox = Box.New(Orientation.Vertical, 10);
        contentBox.SetMarginTop(20);
        contentBox.SetMarginBottom(20);
        contentBox.SetMarginStart(20);
        contentBox.SetMarginEnd(20);

        var label = Label.New(message);
        label.SetWrap(true);
        contentBox.Append(label);

        var closeButton = Button.NewWithLabel("OK");
        closeButton.OnClicked += (s, e) => dialog.Close();
        contentBox.Append(closeButton);

        dialog.SetChild(contentBox);
        dialog.Show();
    }

    private void OnSettingsButtonClicked(Button sender, EventArgs args)
    {
        var dialog = new Dialog();
        dialog.SetTitle("Configuration");
        dialog.SetTransientFor(_window);
        dialog.SetModal(true);
        dialog.SetDefaultSize(600, 460);

        var scrolledWindow = ScrolledWindow.New();
        scrolledWindow.SetPolicy(PolicyType.Never, PolicyType.Automatic);

        var contentBox = Box.New(Orientation.Vertical, 8);
        contentBox.SetMarginTop(8);
        contentBox.SetMarginBottom(8);
        contentBox.SetMarginStart(15);
        contentBox.SetMarginEnd(15);

        // Warning message
        var warningLabel = Label.New("Note: Changes require restart.");
        warningLabel.SetWrap(true);
        warningLabel.SetMarkup("<small><i>Changes require restart</i></small>");
        contentBox.Append(warningLabel);

        // GPIO Pins Section
        var pinsLabel = Label.New("GPIO Pins");
        pinsLabel.SetMarkup("<b><small>GPIO Pins</small></b>");
        pinsLabel.SetHalign(Align.Start);
        pinsLabel.SetMarginTop(3);
        contentBox.Append(pinsLabel);

        // Create horizontal grid for pins (2 columns)
        var pinsGrid = Grid.New();
        pinsGrid.SetRowSpacing(5);
        pinsGrid.SetColumnSpacing(10);
        pinsGrid.SetColumnHomogeneous(true);

        // Row 0: Pulse and Direction
        pinsGrid.Attach(CreateConfigRow("Pulse:", _config.PulsePin.ToString(), val => _config.PulsePin = (int)val, 0, 40), 0, 0, 1, 1);
        pinsGrid.Attach(CreateConfigRow("Direction:", _config.DirectionPin.ToString(), val => _config.DirectionPin = (int)val, 0, 40), 1, 0, 1, 1);

        // Row 1: Min and Max Limit
        pinsGrid.Attach(CreateConfigRow("Min Limit:", _config.MinLimitSwitchPin.ToString(), val => _config.MinLimitSwitchPin = (int)val, 0, 40), 0, 1, 1, 1);
        pinsGrid.Attach(CreateConfigRow("Max Limit:", _config.MaxLimitSwitchPin.ToString(), val => _config.MaxLimitSwitchPin = (int)val, 0, 40), 1, 1, 1, 1);

        // Row 2: Enable (spans both columns)
        pinsGrid.Attach(CreateConfigRow("Enable (opt):", _config.EnablePin?.ToString() ?? "None", val => _config.EnablePin = val == 0 ? null : (int?)val, 0, 40), 0, 2, 2, 1);

        contentBox.Append(pinsGrid);

        // Motor Settings Section
        var motorLabel = Label.New("Motor Settings");
        motorLabel.SetMarkup("<b><small>Motor Settings</small></b>");
        motorLabel.SetHalign(Align.Start);
        motorLabel.SetMarginTop(5);
        contentBox.Append(motorLabel);

        // Create horizontal box for Steps and Lead Screw
        var motorGrid = Grid.New();
        motorGrid.SetRowSpacing(5);
        motorGrid.SetColumnSpacing(10);
        motorGrid.SetColumnHomogeneous(true);

        // Row 0: Steps Per Revolution and Lead Screw
        motorGrid.Attach(CreateConfigDropdownRow("Steps/Rev:", _config.StepsPerRevolution, val => _config.StepsPerRevolution = val), 0, 0, 1, 1);
        motorGrid.Attach(CreateConfigRow("Threads/Inch:", _config.LeadScrewThreadsPerInch.ToString("F2"), val => _config.LeadScrewThreadsPerInch = val, 0.1, 100), 1, 0, 1, 1);

        contentBox.Append(motorGrid);
        contentBox.Append(CreateConfigSliderRow("Acceleration (steps/sec²):", _config.Acceleration, val => _config.Acceleration = val, 1000, 10000));

        // Close button
        var closeButton = Button.NewWithLabel("Close");
        closeButton.SetSizeRequest(-1, 35);
        closeButton.SetMarginTop(10);
        closeButton.OnClicked += (s, e) => dialog.Close();
        contentBox.Append(closeButton);

        scrolledWindow.SetChild(contentBox);
        dialog.SetChild(scrolledWindow);
        dialog.Show();
    }


    private Box CreateConfigRow(string label, string value, Action<double> onValueChanged, double minValue, double maxValue)
    {
        var rowBox = Box.New(Orientation.Horizontal, 5);
        rowBox.SetHexpand(true);

        var labelWidget = Label.New(label);
        labelWidget.SetMarkup($"<small>{label}</small>");
        labelWidget.SetSizeRequest(80, -1);
        labelWidget.SetHalign(Align.Start);
        rowBox.Append(labelWidget);

        var valueEntry = Entry.New();
        valueEntry.SetText(value);
        valueEntry.SetSizeRequest(25, -1);
        valueEntry.SetEditable(false);
        valueEntry.SetCanFocus(true);

        var entryClick = GestureClick.New();
        entryClick.SetPropagationPhase(Gtk.PropagationPhase.Capture);
        entryClick.OnPressed += (sender, args) =>
        {
            var keypadDialog = new Dialog();
            keypadDialog.SetTitle(label);
            keypadDialog.SetTransientFor(_window);
            keypadDialog.SetModal(true);
            keypadDialog.SetDefaultSize(300, 300);

            var keypad = new Keypad();
            keypad.Entry.SetText(valueEntry.GetText());

            keypad.CloseRequested += (s, e) =>
            {
                var inputValue = keypad.Entry.GetText();

                if (!string.IsNullOrWhiteSpace(inputValue) && double.TryParse(inputValue, out var numValue))
                {
                    if (numValue < minValue)
                    {
                        ShowError($"Value must be at least {minValue}");
                        return;
                    }
                    if (numValue > maxValue)
                    {
                        ShowError($"Value must be no more than {maxValue}");
                        return;
                    }

                    valueEntry.SetText(inputValue);
                    onValueChanged(numValue);
                    keypadDialog.Close();
                }
                else if (string.IsNullOrWhiteSpace(inputValue) && label.Contains("opt"))
                {
                    valueEntry.SetText("None");
                    onValueChanged(0);
                    keypadDialog.Close();
                }
                else
                {
                    ShowError("Please enter a valid number");
                }
            };

            keypadDialog.SetChild(keypad);
            keypadDialog.Show();
        };
        valueEntry.AddController(entryClick);

        rowBox.Append(valueEntry);

        return rowBox;
    }

    private Box CreateConfigDropdownRow(string label, StepsPerRevolution currentValue, Action<StepsPerRevolution> onValueChanged)
    {
        var rowBox = Box.New(Orientation.Horizontal, 5);
        rowBox.SetHexpand(true);

        var labelWidget = Label.New(label);
        labelWidget.SetMarkup($"<small>{label}</small>");
        labelWidget.SetSizeRequest(80, -1);
        labelWidget.SetHalign(Align.Start);
        rowBox.Append(labelWidget);

        // Create string list with all enum values
        var stringList = StringList.New(null);
        var enumValues = Enum.GetValues<StepsPerRevolution>();
        uint selectedIndex = 0;

        for (int i = 0; i < enumValues.Length; i++)
        {
            var enumValue = enumValues[i];
            stringList.Append($"{(int)enumValue}");
            if (enumValue == currentValue)
            {
                selectedIndex = (uint)i;
            }
        }

        var dropdown = DropDown.New(stringList, null);
        dropdown.SetSizeRequest(80, -1);
        dropdown.SetSelected(selectedIndex);

        // Handle selection change
        dropdown.OnNotify += (sender, args) =>
        {
            if (args.Pspec.GetName() == "selected")
            {
                var selected = dropdown.GetSelected();
                if (selected < enumValues.Length)
                {
                    onValueChanged(enumValues[selected]);
                }
            }
        };

        rowBox.Append(dropdown);

        return rowBox;
    }

    private Box CreateConfigSliderRow(string label, double currentValue, Action<double> onValueChanged, double minValue, double maxValue)
    {
        var rowBox = Box.New(Orientation.Vertical, 2);
        rowBox.SetHexpand(true);

        // Label row with current value
        var labelBox = Box.New(Orientation.Horizontal, 8);
        labelBox.SetHexpand(true);

        var labelWidget = Label.New(label);
        labelWidget.SetMarkup($"<small>{label}</small>");
        labelWidget.SetHalign(Align.Start);
        labelBox.Append(labelWidget);

        var valueLabel = Label.New($"{currentValue:F0}");
        valueLabel.SetHalign(Align.End);
        valueLabel.SetHexpand(true);
        labelBox.Append(valueLabel);

        rowBox.Append(labelBox);

        // Scale (slider) widget
        var scale = Scale.NewWithRange(Orientation.Horizontal, minValue, maxValue, 100);
        scale.SetValue(currentValue);
        scale.SetDrawValue(false);
        scale.SetHexpand(true);

        // Handle value change
        scale.OnValueChanged += (sender, args) =>
        {
            var newValue = scale.GetValue();
            valueLabel.SetText($"{newValue:F0}");
            onValueChanged(newValue);
        };

        rowBox.Append(scale);

        return rowBox;
    }
}

