using Gtk;
using MotorControllerApp;
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
    private readonly Scale _speedSlider;
    private readonly Label _speedValueLabel;
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
    private const double MIN_RPM = 5;
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

        // Speed and Position input (horizontal layout)
        var inputsBox = Box.New(Orientation.Horizontal, 20);
        inputsBox.SetHexpand(true);
        inputsBox.SetMarginTop(20);
        inputsBox.SetMarginStart(20);
        inputsBox.SetMarginEnd(20);

        // Speed slider section
        var speedInputBox = Box.New(Orientation.Vertical, 5);
        speedInputBox.SetHexpand(true);

        var speedLabelBox = Box.New(Orientation.Horizontal, 10);
        speedLabelBox.SetHexpand(true);
        var speedInputLabel = Label.New("Speed (RPM):");
        speedInputLabel.SetHalign(Align.Start);
        _speedValueLabel = Label.New(_currentRpm.ToString());
        _speedValueLabel.SetHalign(Align.End);
        _speedValueLabel.SetHexpand(true);
        speedLabelBox.Append(speedInputLabel);
        speedLabelBox.Append(_speedValueLabel);

        _speedSlider = Scale.NewWithRange(Orientation.Horizontal, MIN_RPM, MAX_RPM, 1);
        _speedSlider.SetValue(_currentRpm);
        _speedSlider.SetDrawValue(false);
        _speedSlider.SetHexpand(true);
        _speedSlider.OnValueChanged += (sender, args) =>
        {
            _currentRpm = _speedSlider.GetValue();
            _speedValueLabel.SetText($"{_currentRpm:F0}");
            
            // Update motor speed in real-time (works whether motor is moving or not)
            try
            {
                _motorController.SetTargetSpeed(_currentRpm);
            }
            catch (Exception ex)
            {
                // Log error but don't interrupt slider interaction
                Console.WriteLine($"Error setting target RPM: {ex.Message}");
            }
        };

        speedInputBox.Append(speedLabelBox);
        speedInputBox.Append(_speedSlider);

        // Position entry section
        var positionInputBox = Box.New(Orientation.Vertical, 5);
        positionInputBox.SetHalign(Align.Center);

        var positionInputLabel = Label.New("Position (in):");
        positionInputLabel.SetHalign(Align.Start);
        
        _positionEntry = Entry.New();
        _positionEntry.SetText("0");
        _positionEntry.SetSizeRequest(120, -1);
        _positionEntry.SetEditable(false);
        _positionEntry.SetCanFocus(true);

        // Add click handler to show keypad
        var positionEntryClick = GestureClick.New();
        positionEntryClick.SetPropagationPhase(Gtk.PropagationPhase.Capture);
        positionEntryClick.OnPressed += (sender, args) => ShowKeypad(_positionEntry, "Position (inches)", null, null);
        _positionEntry.AddController(positionEntryClick);

        positionInputBox.Append(positionInputLabel);
        positionInputBox.Append(_positionEntry);

        inputsBox.Append(speedInputBox);
        inputsBox.Append(positionInputBox);
        mainBox.Append(inputsBox);

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

    private async void OnMinLimitButtonClicked(Button sender, EventArgs args)
    {
        try
        {
            _moveCts?.Cancel();
            _moveCts = new CancellationTokenSource();

            SetButtonsEnabled(false);
            await _motorController.RunToLimitSwitchAsync(LimitSwitch.Min, _currentRpm, _moveCts.Token);
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
            await _motorController.RunToLimitSwitchAsync(LimitSwitch.Max, _currentRpm, _moveCts.Token);
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
        var powerDialog = new PowerDialog(_window, () => _window.Close());
        powerDialog.Show();
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
        var settingsDialog = new SettingsDialog(_window, _config);
        settingsDialog.Show();
    }
}

