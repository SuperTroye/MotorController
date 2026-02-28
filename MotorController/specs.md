# Stepper Motor Controller
Create a c# class to control a stepper motor driver. This will use System.Device.Gpio to control pins on a Raspberry Pi. 

Create an interface called IGpioController to abstract GPIO operations and to make unit testing easier. It should implement IDisposable and have methods for opening pins, writing to pins, reading from pins, and closing pins.

Create a wrapper class called GpioControllerWrapper that implements IGpioController and uses System.Device.Gpio.GpioController internally.

The pins are for Pulse, Direction, and an optional Enable pin.

Create a class called FakeGpioController that implements IGpioController for debuggin the app on a Windows computer. This class should simulate GPIO operations without interacting with real hardware.


## ControllerConfig 
Create a settings class called ControllerConfig  with the following properties:
- PulsePin (int, default 21)
- DirectionPin (int, default 20)
- EnablePin (int, optional)
- StepsPerRevolution (int, default 400)
- LeadScrewThreadsPerInch (double, default 5)
- Acceleration (double, default 5000)
- MinLimitSwitchPin (int, default 24)
- MaxLimitSwitchPin (int, default 23)

The ControllerConfig class will be used to pass configuration to the StepperMotorController class.


## Acceleration
The StepperMotorController class should incorporate acceleration and deceleration when starting and stopping the motor. Use the David Austin stepper motor algorithm, which provides smooth linear acceleration profile.

The Acceleration property (in steps per second squared) will be used to set motor acceleration.


## Limit Switches
There will be 2 limit switches to constrain maximum and minimum travel.

Create a boolean property for the limit switches (IsMinLimitSwitchTriggered and IsMaxLimitSwitchTriggered) which return true if they are triggered. This will be used by the User Interface.

The limit switches should be read by using GpioController.RegisterCallbackForPinValueChangedEvent.

Create an enum called LimitSwitch with the following values:
- Min - Move toward the minimum limit switch
- Max - Move toward the maximum limit switch

Create a method "RunToLimitSwitchAsync" to run the motor until a limit switch is detected. This method takes a LimitSwitch enum parameter to specify which limit switch to move toward. If the direction is towards the min limit switch, it should set current position to 0. Make sure to incorporate acceleration and deceleration.

As soon as a limit switch is detected, the motor should begin to decelerate. There can be a maximum of 300 steps to decelerate.


## Motor Control
The methods to control motion uses RPM for stepper motor speed.

The stepper motor will turn a lead screw. Use the LeadScrewThreadsPerInch property to set this value and a method to Move, with a parameter of inches. This will divide the desired length by the threads per inch for the distance.

Create a method to stop the motor, which incorporates deceleration.

Create a method to set the current position in inches to zero.

Create a property for current position in inches which is thread safe and can be read at any time.

Create a method MicrosecondSleep to provide accurate timing for pulse generation. This should use a c# Stopwatch, using the Timestamp and number of ticks needed for the delay and a while loop to wait. Use this method in the control loop to delay in between pulses.

Make all methods asynchronous to avoid blocking the main thread.

The Stop method should be able to be called from another thread while the motor is running, so have it set a CancellationToken to stop.

The method to control motion should throw exceptions for invalid parameters, such as moving beyond limit switches or invalid RPM values.

Create a method to adjust the motor speed (rpm) while it is running. This method should update the current speed and recalculate the deceleration profile based on the new speed. Make sure to handle any necessary adjustments to the timing of pulse generation to ensure smooth operation at the new speed.


