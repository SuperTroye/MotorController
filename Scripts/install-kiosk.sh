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

# Install cage if not already installed
if ! command -v cage &> /dev/null; then
    echo "Installing cage Wayland compositor..."
    sudo apt-get update
    sudo apt-get install -y cage
else
    echo "cage is already installed"
fi

# Build the application in Release mode
echo "Building Motor Controller UI..."
cd "$(dirname "$0")"
dotnet restore
dotnet publish UI/UI.csproj -c Release -o /home/$USER/MotorController/UI/bin/Release/net10.0/

# Copy service file
echo "Installing systemd service..."
sudo cp cage-kiosk.service /etc/systemd/system/

# Update service file with current user
sudo sed -i "s/User=pi/User=$USER/g" /etc/systemd/system/cage-kiosk.service

# Update XDG_RUNTIME_DIR with current user ID
USER_ID=$(id -u)
sudo sed -i "s|XDG_RUNTIME_DIR=/run/user/1000|XDG_RUNTIME_DIR=/run/user/$USER_ID|g" /etc/systemd/system/cage-kiosk.service

# Update paths in service file
INSTALL_DIR="$(pwd)"
sudo sed -i "s|/home/pi/MotorController|$INSTALL_DIR|g" /etc/systemd/system/cage-kiosk.service

# Reload systemd
echo "Reloading systemd..."
sudo systemctl daemon-reload

# Enable the service
echo "Enabling cage-kiosk service..."
sudo systemctl enable cage-kiosk.service

echo ""
echo "Installation complete!"
echo ""
echo "Commands:"
echo "  Start kiosk:   sudo systemctl start cage-kiosk"
echo "  Stop kiosk:    sudo systemctl stop cage-kiosk"
echo "  View logs:     sudo journalctl -u cage-kiosk -f"
echo "  Disable:       sudo systemctl disable cage-kiosk"
echo ""
echo "The service will start automatically on next boot."
echo "To start now, run: sudo systemctl start cage-kiosk"
