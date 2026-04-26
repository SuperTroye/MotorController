Currently there is a rotary axis that is driven by gears off the lead screw from the linear axis that is controlled by the StepperMotorController. 

It rotates relative to the rotation of the lead screw.

I want to change this and drive the rotary axis using a stepper motor so that I can chage the ratio without changing gears.

Create a class that incorporates this new stepper motor, as if the rotary axis was driven by gears.

The rotary movoment must be syncronized with the linear movement with no drift.

This should be done by scaling the delay of the rotary axis by the gear ratio. 

Combine the 2 stepper motor pulse generation similar to how LinuxCNC has a stepgen thread that generates pulses for both the linear and rotary axis.

Since the new stepper only turns it won't have a min and max limit switch.

The rotary axis should automatically follow the linear axis's acceleration profile through the synchronization mechanism.

There is already a class for the RotaryAxisConfig, and SynchronizedDualAxisConfig which combines the linear axis configuration and the rotary axis configuration with the gear ratio.