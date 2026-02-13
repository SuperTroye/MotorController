#!/bin/bash
# Installation script for Motor Controller UI Kiosk Mode

set -e

echo "Motor Controller UI - Kiosk Mode Installation"
echo "============================================="
echo ""

# Check if running on Raspberry Pi
if [ ! -f /proc/device-tree/model ] || ! grep -q "Raspberry Pi" /proc/device-tree/model; then
    echo "Warning: This script is designed for Raspberry Pi"
    read -p "Continue anyway? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# Determine script directory
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Install cage if not already installed
if ! command -v cage &> /dev/null; then
    echo "Installing cage Wayland compositor..."
    sudo apt-get update
    sudo apt-get install -y cage
else
    echo "cage is already installed"
fi

# Add user to video and input groups (needed for GPU access)
echo "Adding user to video and input groups..."
sudo usermod -a -G video,input $USER

# Disable graphical boot (X11/Wayland)
echo "Disabling default graphical boot..."
if systemctl is-enabled graphical.target &> /dev/null; then
    sudo systemctl set-default multi-user.target
fi

# Disable lightdm if running
if systemctl is-active lightdm &> /dev/null; then
    echo "Disabling lightdm..."
    sudo systemctl disable lightdm
fi

# Build the application in Release mode
echo "Building Motor Controller UI..."
cd "$PROJECT_ROOT"
dotnet restore
dotnet publish UI/UI.csproj -c Release -r linux-arm64 --self-contained false -o "$HOME/MotorController/UI/bin/Release/net10.0/linux-arm64/publish/"

# Choose service type
echo ""
echo "Select installation type:"
echo "1) System service (recommended for kiosk) - runs on TTY1"
echo "2) User service - runs after user login"
read -p "Enter choice (1 or 2): " -n 1 -r
echo ""

if [[ $REPLY == "1" ]]; then
    # System service installation
    echo "Installing system service..."
    sudo cp "$SCRIPT_DIR/cage.service" /etc/systemd/system/
    
    # Update service file with current user
    sudo sed -i "s/User=troye/User=$USER/g" /etc/systemd/system/cage.service
    
    # Update XDG_RUNTIME_DIR with current user ID
    USER_ID=$(id -u)
    sudo sed -i "s|XDG_RUNTIME_DIR=/run/user/1000|XDG_RUNTIME_DIR=/run/user/$USER_ID|g" /etc/systemd/system/cage.service
    
    # Update paths in service file
    sudo sed -i "s|/home/troye|$HOME|g" /etc/systemd/system/cage.service
    
    # Reload systemd
    echo "Reloading systemd..."
    sudo systemctl daemon-reload
    
    # Enable the service
    echo "Enabling cage service..."
    sudo systemctl enable cage.service
    
    echo ""
    echo "System service installation complete!"
    echo ""
    echo "Commands:"
    echo "  Start kiosk:   sudo systemctl start cage"
    echo "  Stop kiosk:    sudo systemctl stop cage"
    echo "  View logs:     sudo journalctl -u cage -f"
    echo "  Disable:       sudo systemctl disable cage"
    
elif [[ $REPLY == "2" ]]; then
    # User service installation
    echo "Installing user service..."
    mkdir -p "$HOME/.config/systemd/user"
    cp "$SCRIPT_DIR/cage-user.service" "$HOME/.config/systemd/user/"
    
    # Update paths in user service file
    sed -i "s|%h/MotorController|$HOME/MotorController|g" "$HOME/.config/systemd/user/cage-user.service"
    
    # Enable lingering for user (allows user services to run without login)
    sudo loginctl enable-linger $USER
    
    # Reload user systemd
    echo "Reloading user systemd..."
    systemctl --user daemon-reload
    
    # Enable the user service
    echo "Enabling cage-user service..."
    systemctl --user enable cage-user.service
    
    echo ""
    echo "User service installation complete!"
    echo ""
    echo "Commands:"
    echo "  Start kiosk:   systemctl --user start cage-user"
    echo "  Stop kiosk:    systemctl --user stop cage-user"
    echo "  View logs:     journalctl --user -u cage-user -f"
    echo "  Disable:       systemctl --user disable cage-user"
else
    echo "Invalid choice. Exiting."
    exit 1
fi

echo ""
echo "IMPORTANT: You need to log out and log back in for group changes to take effect."
echo "After re-login, the service will start automatically on next boot."
echo ""
echo "To start now (after re-login):"
if [[ $REPLY == "1" ]]; then
    echo "  sudo systemctl start cage"
else
    echo "  systemctl --user start cage-user"
fi
