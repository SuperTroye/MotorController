using Gtk;
using MotorControllerApp;

namespace Widgets;

public class SettingsDialog
{
    private readonly Window _parentWindow;
    private readonly ControllerConfig _config;
    private readonly Dialog _dialog;

    public SettingsDialog(Window parentWindow, ControllerConfig config)
    {
        _parentWindow = parentWindow;
        _config = config;

        _dialog = new Dialog();
        _dialog.SetTitle("Configuration");
        _dialog.SetTransientFor(_parentWindow);
        _dialog.SetModal(true);
        _dialog.SetDefaultSize(600, 460);

        BuildDialog();
    }

    public void Show()
    {
        _dialog.Show();
    }

    private void BuildDialog()
    {
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
        closeButton.OnClicked += (s, e) => _dialog.Close();
        contentBox.Append(closeButton);

        scrolledWindow.SetChild(contentBox);
        _dialog.SetChild(scrolledWindow);
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
            keypadDialog.SetTransientFor(_parentWindow);
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

    private void ShowError(string message)
    {
        var dialog = new Dialog();
        dialog.SetTitle("Error");
        dialog.SetTransientFor(_parentWindow);
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
}
