using OpenTabletDriver;
using OpenTabletDriver.Native.Windows.Input;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Platform.Display;
using OpenTabletDriver.Plugin.Tablet;
using System.Numerics;
using System.Xml.Linq;

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
            //LogInfo();
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


    [Resolved]
    public IDriver driver;
    private OutputModeType output_mode_type = OutputModeType.unknown;
    private AbsoluteOutputMode? absolute_output_mode = null;
    private RelativeOutputMode? relative_output_mode = null;
    private void try_resolve_output_mode()
    {
        Log.Write("Info", "Running try_resolve_output_mode()");
        if (driver is Driver drv)
        {
            IOutputMode output = drv.InputDevices
                .Where(dev => dev?.OutputMode?.Elements?.Contains(this) ?? false)
                .Select(dev => dev?.OutputMode).FirstOrDefault();

            LogInputDevices();

            if (output is AbsoluteOutputMode abs_output)
            {
                absolute_output_mode = abs_output;
                output_mode_type = OutputModeType.absolute;
            }
            else if (output is RelativeOutputMode rel_output)
            {
                relative_output_mode = rel_output;
                output_mode_type = OutputModeType.relative;
            }
            else
                output_mode_type = OutputModeType.unknown;
            Log.Write("SkewArea",  $"output is of type {output?.GetType().ToString() ?? "null"}, So OutputModeType = {output_mode_type}");
            Log.Write("Info", "Finished running try_resolve_output_mode()");
        }
    }

    private void LogInfo()
    {
        LogInputDevices();
        Log.Write("SkewArea", "Logging SkewArea info...");
        Log.Write("Area", $"skew matrix for SkewAngleY = {_skewAngleY}: " +
                $"\n{skewMatrix.M11}| {skewMatrix.M12}" +
                $"\n{skewMatrix.M21}| {skewMatrix.M22}" +
                $"\n{skewMatrix.M31}| {skewMatrix.M32}" +
                $"\nwhich for an input (10, 10) gives {Skew(new Vector2(10, 10))}");
                //$"\nwhich gives the following inner rectangle: x= {inner_x_min} .. {inner_x_max} and y = {area_y_min} .. {area_y_max}");
    }
    private void LogInputDevices()
    {
        Log.Write("Info", "Logging input devices...");
        if (driver is Driver drv)
        {
            foreach (var dev in drv.InputDevices)
            {
                Log.Write("Info", $"InputDevice \"{nameof(dev)}\" with type {dev.GetType()}");
                Log.Write("Info", $"and Output mode dev.OutputMode {dev.OutputMode} with type {dev.OutputMode?.GetType()}\"");
                if (dev.OutputMode == null)
                    continue;
                Log.Write("Info", $"and dev.OutputMode.Elements:");
                if (dev.OutputMode.Elements == null)
                {
                    Log.Write("Info", $"dev.OutputMode.Elements is null...");
                    continue;
                }
                foreach (var element in dev.OutputMode.Elements)
                {
                    Log.Write("Info", $"element {element} of type {element.GetType()}");
                }
            }
        }
        Log.Write("Info", "Finished logging input devices.");
    }

    float area_x_min;
    float area_x_max;
    float area_y_min;
    float area_y_max;
    bool tabletAreaExists = false;

    private static bool isFirstTimeWeHaveAbsoluteOutputMode = true;

    /// <summary>
    /// Attempts to define the corners of the tablet area.
    /// </summary>
    private void GetTabletArea()
    {
        if (output_mode_type is OutputModeType.absolute)
        {
            if (isFirstTimeWeHaveAbsoluteOutputMode)
            {
                Log.Write("SkewArea", "First time encountering absolute output mode. Logging...");
                LogInfo();
                Log.Write("SkewArea", "Now attempting to set var absOutput = absolute_output_mode" +
                    $"\nWe have absolute_output_mode = {absolute_output_mode?.ToString() ?? "null"}");
            }
            var absOutput = absolute_output_mode;
            if (absOutput == null)
                return;

            //if (isFirstTimeWeHaveAbsoluteOutputMode)
            //{
            //    Log.Write("SkewArea", $"Now attempting to set var display = absOutput.Output. " +
            //        $"\nWe have absOutput?.Output = {absOutput?.Output.ToString() ?? "null"}");
            //}
            var display = absOutput.Output;

            //if (isFirstTimeWeHaveAbsoluteOutputMode)
            //{
            //    Log.Write("SkewArea", "Now attempting to set var offset = absOutput.Output.Position");
            //}
            Vector2 offset = absOutput.Output.Position;

            area_x_min = offset.X - (display.Width / 2);
            area_x_max = offset.X + (display.Width / 2);
            area_y_min = offset.Y - (display.Height / 2);
            area_y_max = offset.Y + (display.Height / 2);
            tabletAreaExists = true;
            if (isFirstTimeWeHaveAbsoluteOutputMode)
            {
                Log.Write($"SkewArea", "We have the following tablet area coordinates:" +
                    $"\nx = {area_x_min} .. {area_x_max} and y= {area_y_min} .. {area_y_max}");
                Log.Write("SkewArea", "Getting inner rectangle...");
                GetInnerRectangle();
                Log.Write($"SkewArea", "We have the following inner rectangle area coordinates:" +
                    $"\nx= {inner_x_min} .. {inner_x_max} and y = {area_y_min} .. {area_y_max}");

                Log.Write("SkewArea", "Finished logging info upon first time encountering absolute output mode.");
                isFirstTimeWeHaveAbsoluteOutputMode = false;
            }
            return;
        }
        try_resolve_output_mode();
        tabletAreaExists = false;
    }

    float inner_x_min, inner_x_max;


    /// <summary>
    /// Attempts to define the corners of the inner rectangle (which will be skewed by the skew matrix to yield the inner parallelogram).
    /// </summary>
    /// <returns>Whether operation succeeded</returns>
    private bool GetInnerRectangle()
    {
        if (!tabletAreaExists)
        {
            GetTabletArea();
            if (!tabletAreaExists)
                return false;
        }

        // Calculate the horitonzal edge length of the inner parallelogram
        float paraHorizontalEdgeLength = (area_x_max - area_x_min) - Math.Abs((float)Math.Tan(SkewAngleY * Math.PI / 180)) * (area_y_max - area_y_min);

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
        if (!GetInnerRectangle())
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
        if (output_mode_type is OutputModeType.absolute)
        {
            Vector2? parallelogramCoordinate = AreaToInnerRectangle(input);
            if (parallelogramCoordinate == null)
                return input;
            else
                return (Vector2)parallelogramCoordinate;
        }
        try_resolve_output_mode();
        return input;
    }
}
enum OutputModeType
{
    absolute,
    relative,
    unknown
}
