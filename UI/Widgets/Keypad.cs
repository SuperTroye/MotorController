using Gtk;

namespace Widgets;

public class Keypad
{
    private readonly Entry _entry;
    private readonly Box _root;

    public Entry Entry => _entry;
    public Widget Widget => _root;
    public event EventHandler? CloseRequested;

    public Keypad()
    {
        // Use an internal Box instead of inheriting from Box
        _root = Box.New(Orientation.Vertical, 10);
        _root.SetSpacing(10);
        _root.SetMarginStart(20);
        _root.SetMarginEnd(20);
        _root.SetMarginTop(20);
        _root.SetMarginBottom(20);

        // Create entry field
        _entry = Entry.New();
        _entry.SetHexpand(true);
        _entry.SetPlaceholderText("Enter value");
        _entry.SetAlignment(0.5f); // Center text
        _entry.SetEditable(false); // Prevent keyboard input

        // Style the entry
        _entry.AddCssClass("keypad-entry");

        _root.Append(_entry);

        // Create grid for buttons
        var grid = Grid.New();
        grid.SetRowSpacing(10);
        grid.SetColumnSpacing(10);
        grid.SetHexpand(true);
        grid.SetVexpand(true);
        grid.SetHalign(Align.Fill);
        grid.SetValign(Align.Fill);

        for (int i = 1; i <= 9; i++)
        {
            int digit = i;
            var button = CreateNumberButton(digit.ToString());
            button.OnClicked += (sender, args) => AppendDigit(digit.ToString());

            int row = (i - 1) / 3;
            int col = (i - 1) % 3;
            grid.Attach(button, col, row, 1, 1);
        }

        var backspaceButton = CreateControlButton("⌫");
        backspaceButton.OnClicked += (sender, args) => Backspace();
        backspaceButton.AddCssClass("backspace-button");
        grid.Attach(backspaceButton, 0, 3, 1, 1);

        var zeroButton = CreateNumberButton("0");
        zeroButton.OnClicked += (sender, args) => AppendDigit("0");
        grid.Attach(zeroButton, 1, 3, 1, 1);

        var periodButton = CreateNumberButton(".");
        periodButton.OnClicked += (sender, args) => AppendPeriod();
        grid.Attach(periodButton, 2, 3, 1, 1);

        var confirmButton = CreateControlButton("✓");
        confirmButton.OnClicked += (sender, args) => OnCloseRequested();
        confirmButton.AddCssClass("confirm-button");
        grid.Attach(confirmButton, 3, 3, 1, 1);

        _root.Append(grid);

        var closeBox = Box.New(Orientation.Horizontal, 5);
        closeBox.SetHalign(Align.Center);

        var closeButton = Button.New();
        var closeLabel = Label.New("✕ Cancel");
        closeLabel.SetHalign(Align.Center);
        closeButton.SetChild(closeLabel);
        closeButton.OnClicked += (sender, args) => OnCloseRequested();
        closeButton.AddCssClass("close-button");
        closeButton.SetSizeRequest(200, 50);

        closeBox.Append(closeButton);
        _root.Append(closeBox);
    }

    private Button CreateNumberButton(string text)
    {
        var button = Button.New();
        var label = Label.New(text);
        label.SetHalign(Align.Center);
        button.SetChild(label);
        button.SetHexpand(true);
        button.SetVexpand(true);
        button.SetSizeRequest(80, 60);
        button.AddCssClass("keypad-number-button");
        return button;
    }

    private Button CreateControlButton(string text)
    {
        var button = Button.New();
        var label = Label.New(text);
        label.SetHalign(Align.Center);
        button.SetChild(label);
        button.SetHexpand(true);
        button.SetVexpand(true);
        button.SetSizeRequest(80, 60);
        button.AddCssClass("keypad-control-button");
        return button;
    }

    private void AppendDigit(string digit)
    {
        var currentText = _entry.GetText() ?? string.Empty;

        if (currentText == "0")
            _entry.SetText(digit);
        else
            _entry.SetText(currentText + digit);

        _entry.SetPosition(-1);
    }

    private void AppendPeriod()
    {
        var currentText = _entry.GetText() ?? string.Empty;
        if (!currentText.Contains('.'))
        {
            _entry.SetText(currentText.Length == 0 ? "0." : currentText + ".");
            _entry.SetPosition(-1);
        }
    }

    private void Backspace()
    {
        var currentText = _entry.GetText() ?? string.Empty;
        if (currentText.Length > 0)
        {
            _entry.SetText(currentText.Substring(0, currentText.Length - 1));
            _entry.SetPosition(-1);
        }
    }

    public void Clear() => _entry.SetText(string.Empty);

    private void OnCloseRequested() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
