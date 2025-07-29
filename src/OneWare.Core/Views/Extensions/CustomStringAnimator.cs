using Avalonia.Animation;

namespace OneWare.Core.Views.Extensions;

public class CustomStringAnimator : InterpolatingAnimator<string>
{
    public override string Interpolate(double progress, string oldValue, string newValue)
    {
        if (newValue.Length == 0) return "";
        var step = 1.0 / newValue.Length;
        var length = (int)(progress / step);
        var result = newValue[..(length + 1)];
        return result;
    }
}