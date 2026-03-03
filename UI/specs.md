# GUI
Create a small c# GTK4 application using GirCore.Gtk-4.0.

The ApplicationWindow size should fit a Raspberry Pi 5 inch touchscreen 800x480 display.

Create 2 buttons to run to min and max limit switches. They should have a left arrow icon (to move toward min limit switch) and right arrow icon (to move toward max limit switch) respectively. These will call the RunToLimitSwitchAsync method in the StepperMotorController class.

Create 2 circles to show the minimum and maximum limit switch status. This should check the boolean properties (IsMinLimitSwitchTriggered and IsMaxLimitSwitchTriggered) in the StepperMotorController class. If the limit switch is triggered, the circle should be red, otherwise green. Instead of polling the UI in a timeout, use the events from the StepperMotorController class (MinLimitSwitchTriggered and MaxLimitSwitchTriggered) to change the colors. 

Create 2 labels to show motor speed in RPM and current position in inches.

Create a text input to set the desired speed in RPM. Validate the input to ensure it is a valid number and within acceptable range for the motor (e.g., 0-350 RPM).

Create a text input to set the desired position in inches. Validate the input to ensure it is a valid number and within the travel limits defined by the limit switches.

Create a button to Move to the desired position. This will call the MoveInchesAsync method in the StepperMotorController class with the value from the text input.

Create a button to Stop the motor. This will call the StopAsync method in the StepperMotorController class.

Make sure to use the latest version of GirCore.Gtk-4.0 which references GTK4. All design should follow GTK4 best practices.


# Fine Adjustment
Add 2 buttons to the upper right portion of the Main Window. 
The buttons are for making fine adjustments to the motor speed. 
Each click of the button would increment or decrement the speed by 1 RPM. 
The increment button should have an up arrow icon and the decrement button should have a down arrow icon. 
Use an image from the internet if needed. 
Use the provided an imge for reference.


# Power Dialog Box
Create a button to open a dialog for shutting down or restarting the Raspberry Pi. 
The dialog should have options for "Shutdown", "Restart", "Cancel", and "Exit Application". 
The "Shutdown" and "Restart" options should execute the appropriate system commands to perform these actions.


The UI app will be running in Kiosk mode without a virtual keyboard, so it will need to account for user input. Create a Keypad widget to accept numeric input for setting the RPM and Position. Use a Grid and add buttons for 0-9, Backspace, and Clear. Connect the clicked signal of each button to append text to a Entry.

# Settings Screen
Create a screen to view and edit the ControllerConfig values.
The StepsPerRevolution property is an enum, so use a dropdown of the enum values.
Use a gtk range slider to set the Acceleration with a range of 1000 to 10000.


Make sure to build the solution to make sure there are no errors.


# Kiosk Mode
Create a service unit file that uses cage kiosk mode and sets the default application to the UI project.