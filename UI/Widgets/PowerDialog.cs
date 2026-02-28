using Gtk;
using System.Diagnostics;

namespace Widgets;

public class PowerDialog
{
    private readonly Window _parentWindow;
    private readonly Dialog _dialog;
    private readonly Action _onCloseApp;

    public PowerDialog(Window parentWindow, Action onCloseApp)
    {
        _parentWindow = parentWindow;
        _onCloseApp = onCloseApp;

        _dialog = new Dialog();
        _dialog.SetTitle("System Power");
        _dialog.SetTransientFor(_parentWindow);
        _dialog.SetModal(true);
        _dialog.SetDefaultSize(300, 200);

        BuildDialog();
    }

    public void Show()
    {
        _dialog.Show();
    }

    private void BuildDialog()
    {
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
            _dialog.Close();
            _onCloseApp();
        };
        contentBox.Append(closeAppButton);

        var shutdownButton = Button.NewWithLabel("Shutdown");
        shutdownButton.SetSizeRequest(-1, 50);
        shutdownButton.OnClicked += (s, e) =>
        {
            _dialog.Close();
            ExecuteSystemCommand("shutdown", "/s /t 0");
        };
        contentBox.Append(shutdownButton);

        var restartButton = Button.NewWithLabel("Restart");
        restartButton.SetSizeRequest(-1, 50);
        restartButton.OnClicked += (s, e) =>
        {
            _dialog.Close();
            ExecuteSystemCommand("shutdown", "/r /t 0");
        };
        contentBox.Append(restartButton);

        var cancelButton = Button.NewWithLabel("Cancel");
        cancelButton.SetSizeRequest(-1, 50);
        cancelButton.OnClicked += (s, e) => _dialog.Close();
        contentBox.Append(cancelButton);

        _dialog.SetChild(contentBox);
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
