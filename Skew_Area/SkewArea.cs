﻿using OpenTabletDriver;
using OpenTabletDriver.Native.Windows.Input;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Platform.Display;
using OpenTabletDriver.Plugin.Tablet;
using System.Numerics;

namespace Skew_Area;

[PluginName("Skew Area")]
public class SkewArea : IPositionedPipelineElement<IDeviceReport>
{
    #region Skew
    private float _skewAngleY;
    [Property("Y Angle"), ToolTip("The angle in degrees to rotate the Y-axis counter-clockwise"), DefaultPropertyValue(0f)]
    public float SkewAngleY
    {
        set
        {
            _skewAngleY = Math.Clamp(value, -60, 60);
            skewMatrix = Matrix3x2.CreateSkew((float)(_skewAngleY * Math.PI / 180), 0);
            LogInfo();
        }

        get => _skewAngleY;
    }

    public Matrix3x2 skewMatrix = Matrix3x2.Identity;

    public Vector2 Skew(Vector2 input) => Vector2.Transform(input, skewMatrix);
    #endregion

    #region Essential Methods
    public void Consume(IDeviceReport value)
    {
        if (value is ITabletReport report)
        {
            report.Position = Filter(report.Position);
        }
        Emit?.Invoke(value);
    }

    public event Action<IDeviceReport>? Emit;

    public PipelinePosition Position => PipelinePosition.PostTransform; 
    #endregion

    private void LogInfo()
    {
        LogInputDevices();
        Log.Write("SkewArea", "Logging SkewArea info...");
        Log.Write("SkewArea", $"assertOutputModeIsAbsolute() = {assertOutputModeIsAbsolute()}");
        Log.Write("SkewArea", $"getTabletArea() = {getTabletArea()}");
        Log.Write("SkewArea", $"getInnerRectangle() = {getInnerRectangle()}");
        Log.Write("Area", $"skew matrix for SkewAngleY = {_skewAngleY}: " +
                $"\n{skewMatrix.M11}| {skewMatrix.M12}" +
                $"\n{skewMatrix.M21}| {skewMatrix.M22}" +
                $"\n{skewMatrix.M31}| {skewMatrix.M32}" +
                $"\nwhich for an input (10, 10) gives {Skew(new Vector2(10, 10))}" +
                $"\nand we have the tablet area coordinates x = {area_x_min} .. {area_x_max} and y= {area_y_min} .. {area_y_max}" +
                $"\nwhich gives the following inner rectangle: x= {inner_x_min} .. {inner_x_max} and y = {area_y_min} .. {area_y_max}");
    }

    [Resolved]
    public IDriver driver = null!;
    private AbsoluteOutputMode? outputMode;
    /// <summary>
    /// If the output mode is absolute, saves the <see cref="AbsoluteOutputMode"/> variable as <see cref="outputMode"/>. 
    /// This is required in order to obtain the tablet area's dimensions and offset.
    /// </summary>
    /// <returns>Whether the output mode is absolute</returns>
    /// <exception cref="InvalidOperationException"></exception>
    private bool assertOutputModeIsAbsolute()
    {
        if (driver == null)
        {
            Log.Write("SkewArea", "driver was null");
            throw new InvalidOperationException("driver was null");
        }
        if (driver is Driver drv)
        {
            IOutputMode? output = drv.InputDevices
                .Where(dev => dev?.OutputMode?.Elements?.Contains(this) ?? false)
                .Select(dev => dev?.OutputMode).FirstOrDefault();

            if (output is AbsoluteOutputMode absOutput)
            {
                outputMode = absOutput;
                return true;
            }
            else if (output is null)
            {
                Log.Write("SkewArea", $"output was null");
                return false;
            }
            else
            {
                Log.Write("SkewArea", $"output was not AbsoluteOutputMode but was {output.GetType()}");
                return false;
            }
        }
        else
        {
            Log.Write("SkewArea", "driver was not of type Driver");
            return false;
        }
    }

    private void LogInputDevices()
    {
        Log.Write("Info", "Logging input devices...");
        if (driver is Driver drv)
        {
            foreach (var dev in drv.InputDevices)
            {
                Log.Write("Info", $"InputDevice \"{nameof(dev)}\" with type {dev.GetType()} " +
                    $"\nand Output mode dev.OutputMode {dev.OutputMode} with type {dev.OutputMode?.GetType()}" +
                    $"\nand dev.OutputMode.Elements {dev.OutputMode?.Elements} with type {dev.OutputMode?.Elements.GetType()}");
            }
        }
    }

    float area_x_min;
    float area_x_max;
    float area_y_min;
    float area_y_max;


    /// <summary>
    /// Attempts to define the corners of the tablet area.
    /// </summary>
    /// <returns>Whether operation succeeded</returns>
    private bool getTabletArea()
    {
        assertOutputModeIsAbsolute();
        if (outputMode is not null)
        {
            var display = outputMode.Output;
            var offset = outputMode.Output.Position;
            var shiftoffX = offset.X - (display.Width / 2);
            var shiftoffY = offset.Y - (display.Height / 2);
            area_x_min = offset.X - (display.Width / 2);
            area_x_max = offset.X + (display.Width / 2);
            area_y_min = offset.Y - (display.Height / 2);
            area_y_max = offset.Y + (display.Height / 2);
            return true;
        }
        return false;
    }

    float inner_x_min, inner_x_max;


    /// <summary>
    /// Attempts to define the corners of the inner rectangle (which will be skewed by the skew matrix to yield the inner parallelogram).
    /// </summary>
    /// <returns>Whether operation succeeded</returns>
    private bool getInnerRectangle()
    {
        if (!getTabletArea())
            return false;

        // Calculate the horitonzal edge length of the inner parallelogram
        float paraHorizontalEdgeLength = (area_x_max - area_x_min) - (float)Math.Tan(SkewAngleY * Math.PI / 180) * (area_y_max - area_y_min);

        if (SkewAngleY > 0)
        {
            inner_x_min = area_x_max - paraHorizontalEdgeLength;
            inner_x_max = area_x_max;
        }
        else
        {
            inner_x_min = area_x_min;
            inner_x_max = area_x_min + paraHorizontalEdgeLength;
        }

        return true;
    }

    /// <summary>
    /// Takes a point from the tablet area and transforms it to reside within the inner rectangle
    /// </summary>
    /// <returns>The coordinate transformed to the inner rectangle. Null if there's no absolute tablet area</returns>
    private Vector2? AreaToInnerRectangle(Vector2 input)
    {
        if (!getInnerRectangle())
            return null;
        float scaleX = (inner_x_max - inner_x_min) / (area_x_max - area_x_min);

        float offsetInputXToOrigin = SkewAngleY > 0 ? -area_x_max : -area_x_min;
        Vector2 offsetInputToOrigin = new Vector2(offsetInputXToOrigin, 0);
        Vector2 inputAtOrigin = input + offsetInputToOrigin;
        Vector2 outputAtOrigin = new Vector2(inputAtOrigin.X * scaleX, inputAtOrigin.Y);
        Vector2 output = outputAtOrigin - offsetInputToOrigin;

        return output;
    }

    /// <summary>
    /// Takes a point from the tablet area and transforms it to reside within the inner parallelogram
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private Vector2? AreaToInnerParallelogram(Vector2 input)
    {
        Vector2? innerRectangleCoordinate = AreaToInnerRectangle(input);
        if (innerRectangleCoordinate == null)
            return null;

        Vector2 parallelogramCoordinate = Skew((Vector2)innerRectangleCoordinate);

        return parallelogramCoordinate;
    }

    /// <summary>
    /// The final filter
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private Vector2 Filter(Vector2 input)
    {
        Vector2? parallelogramCoordinate = AreaToInnerRectangle(input);
        if (parallelogramCoordinate == null)
            return input;
        else
            return (Vector2)parallelogramCoordinate;
    }
}
