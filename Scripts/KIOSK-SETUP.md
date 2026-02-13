# Kiosk Mode Setup for Motor Controller UI

This directory contains files for setting up the Motor Controller UI application to run in kiosk mode on a Raspberry Pi using the cage Wayland compositor.

## Files

- `cage-kiosk.service` - Systemd service unit file for running the application in kiosk mode
- `install-kiosk.sh` - Installation script that automates the setup process

## Prerequisites

- Raspberry Pi running Raspberry Pi OS (Bullseye or later)
- .NET 10 SDK installed
- Wayland support (default on recent Raspberry Pi OS versions)
- Motor controller hardware properly connected

## Manual Installation

If you prefer to install manually instead of using the script:

1. **Install cage compositor:**
   ```bash
   sudo apt-get update
   sudo apt-get install cage
   ```

2. **Build the application:**
   ```bash
   dotnet restore
   dotnet publish UI/UI.csproj -c Release
   ```

3. **Copy the service file:**
   ```bash
   sudo cp cage-kiosk.service /etc/systemd/system/
   ```

4. **Edit the service file** to update paths and user:
   ```bash
   sudo nano /etc/systemd/system/cage-kiosk.service
   ```
   
   Update these lines:
   - `User=` to your username (e.g., `pi`)
   - `XDG_RUNTIME_DIR=` to match your user ID (usually `/run/user/1000` for the default `pi` user)
   - Update the path in `ExecStart=` to match your installation directory

5. **Enable and start the service:**
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable cage-kiosk.service
   sudo systemctl start cage-kiosk.service
   ```

## Automated Installation

Simply run the installation script:

```bash
chmod +x install-kiosk.sh
./install-kiosk.sh
```

The script will:
- Install cage if needed
- Build the application in Release mode
- Install and configure the systemd service
- Enable the service to start on boot

## Service Management

After installation, use these commands to manage the kiosk:

- **Start the kiosk:** `sudo systemctl start cage-kiosk`
- **Stop the kiosk:** `sudo systemctl stop cage-kiosk`
- **Restart the kiosk:** `sudo systemctl restart cage-kiosk`
- **View logs:** `sudo journalctl -u cage-kiosk -f`
- **Disable auto-start:** `sudo systemctl disable cage-kiosk`
- **Check status:** `sudo systemctl status cage-kiosk`

## Troubleshooting

### Application doesn't start

1. Check the service logs:
   ```bash
   sudo journalctl -u cage-kiosk -f
   ```

2. Verify .NET is installed:
   ```bash
   dotnet --version
   ```

3. Test the application manually:
   ```bash
   cd UI/bin/Release/net10.0
   dotnet UI.dll
   ```

### Display issues

- Ensure you're using a Wayland-compatible display
- Check that the HDMI display is connected before boot
- Verify `XDG_RUNTIME_DIR` is set correctly in the service file

### Permission errors

- Ensure the user in the service file has access to GPIO
- Add user to `gpio` group: `sudo usermod -a -G gpio $USER`
- Reboot after adding to groups

### Exiting kiosk mode

If you need to exit kiosk mode to access the shell:

1. From another SSH session: `sudo systemctl stop cage-kiosk`
2. Or disable at boot: `sudo systemctl disable cage-kiosk`

## Configuration

### Custom Installation Path

If you install the application in a different location, update the path in `/etc/systemd/system/cage-kiosk.service`:

```ini
ExecStart=/usr/bin/cage -- /usr/share/dotnet/dotnet /your/custom/path/UI/bin/Release/net10.0/UI.dll
```

### Different Display Resolution

The application is designed for 800x480 displays. If using a different resolution, you may need to modify the UI code in `UI/Program.cs`.

### Auto-login (Optional)

For a fully automated kiosk, configure auto-login:

```bash
sudo raspi-config
```

Navigate to: System Options ? Boot / Auto Login ? Console Autologin

## Security Considerations

- The kiosk runs with user privileges (not root)
- The shutdown/restart buttons in the UI require appropriate sudo permissions
- Consider adding password protection for shutdown/restart functionality in production
- Restrict SSH access if the kiosk is deployed in public areas

## Notes

- The service is configured to automatically restart if it crashes
- A 3-second delay is configured between restart attempts
- The application will start automatically on boot after installation
- Cage provides a minimal Wayland compositor ideal for kiosk applications
