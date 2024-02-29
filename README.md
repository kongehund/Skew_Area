A plugin for OpenTabletDriver which creates a parallelogram inside the display area based on an angle SkewYAngle.
Enabling the filter will constrain the input area to this parallelogram. The input is then translated to fit the entire display area.

The parallelogram has flat bottom and top edges, and the Y-axis is rotated by an angle SkewYAngle (angle between vertical axis and desired parallelogram angle). The parallelogram will automatically reside within the input area unless you choose an angle that's too large.

The plugin is in a usable state, but the following should be taken into account:
- There's no limit to the angle you choose, so the parallelogram won't stay within the display area if the angle is greater than the angle to the diagonal line.
- There's some math errors, so the angle you choose isn't the same as the actual angle. This isn't a problem unless you want your parallelogram to have a specific angle instead of experimenting to find one that works for you.
