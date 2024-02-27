using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using System.Numerics;

namespace Skew_Area;

[PluginName("Skew Area")]
public class SkewArea : IPositionedPipelineElement<IDeviceReport>
{
    private float _skewAngleY;
    [Property("Y Angle"), ToolTip("The angle in degrees to rotate the Y-axis clockwise"), DefaultPropertyValue(0f)]
    public float SkewAngleY
    {
        set
        {
            _skewAngleY = Math.Clamp(value, -60, 60);
            _skewMatrix = Matrix3x2.CreateSkew(0, (float)(_skewAngleY * Math.PI / 180));
        }

        get => _skewAngleY;
    }

    private Matrix3x2 _skewMatrix = Matrix3x2.Identity;

    public event Action<IDeviceReport>? Emit;

    public void Consume(IDeviceReport value)
    {
        if (value is ITabletReport report)
        {
            report.Position = Skew(report.Position);
        }
        Emit?.Invoke(value);
    }

    public Vector2 Skew(Vector2 oldPosition)
    {
        return Vector2.Transform(oldPosition, _skewMatrix);
    }

    public PipelinePosition Position => PipelinePosition.PostTransform;
}

