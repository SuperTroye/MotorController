#!/bin/bash

# ============================================
# Update and Publish MotorController
# ============================================

set -e  # Exit on any error

REPO_DIR="$HOME/MotorController"
PROJECT_PATH="$REPO_DIR/UI/UI.csproj"

echo "========================================"
echo "Updating MotorController Application"
echo "========================================"

# Change to repository directory
echo "Changing to repository directory..."
cd "$REPO_DIR"

# Pull latest changes from git
echo "Pulling latest changes from git..."
git pull

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore

# Publish the application
echo "Publishing application for linux-arm64..."
dotnet publish "$PROJECT_PATH" -c Release -r linux-arm64 --self-contained true

# Make the executable
echo "Setting executable permissions..."
chmod +x "$REPO_DIR/UI/bin/Release/net10.0/linux-arm64/publish/UI"

echo "========================================"
echo "Update and publish completed successfully!"
echo "========================================"
