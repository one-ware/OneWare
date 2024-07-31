using System.Globalization;
using System.Numerics;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Essentials.Helpers;
using OneWare.WaveFormViewer.ViewModels;

namespace OneWare.WaveFormViewer.Controls;

public class Marker
{
    public long Position { get; set; }
    public bool IsMainMarker { get; set; }
}

public class WaveFormScale : Control
{
    private readonly IPen _markerBrushPen;

    private CompositeDisposable _disposableReg = new();
    
    private const int MinScaleNumberWidth = 150;

    public WaveFormScale()
    {
        ClipToBounds = true;
        var markerBrush = (IBrush)new BrushConverter().ConvertFrom("#575151")!;
        _markerBrushPen = new Pen(markerBrush, 2);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _disposableReg.Dispose();
        _disposableReg = new CompositeDisposable();

        if (DataContext is WaveFormViewModel vm)
        {
            vm.WhenValueChanged(x => x.Max).Subscribe(x => { Redraw(); }).DisposeWith(_disposableReg);
            vm.WhenValueChanged(x => x.Offset).Subscribe(x => { Redraw(); }).DisposeWith(_disposableReg);
            vm.WhenValueChanged(x => x.ZoomMultiply).Subscribe(x => { Redraw(); }).DisposeWith(_disposableReg);
            vm.WhenValueChanged(x => x.TimeScale).Subscribe(x => { Redraw(); }).DisposeWith(_disposableReg);
        }
    }

    public static List<Marker> CalculateMarkers(long beginOffset, long endOffset, int maxMainMarkers)
{
    var markers = new List<Marker>();
    
    if (beginOffset >= endOffset || maxMainMarkers <= 0)
        return markers;

    var range = (double)(endOffset - beginOffset);
    var mainStep = FindBeautifulStep(range, maxMainMarkers);
    var intermediateStep = (long)(mainStep / 10);
    
    var firstMainMarker = Math.Ceiling(beginOffset / mainStep) * mainStep;
    
    for (var i = firstMainMarker; i >= beginOffset; i -= intermediateStep)
    {
        markers.Insert(0, new Marker { Position = (long)i, IsMainMarker = false });
    }

    for (var mainMarker = (long)firstMainMarker; mainMarker <= endOffset; mainMarker += (long)mainStep)
    {
        markers.Add(new Marker { Position = mainMarker, IsMainMarker = true });

        // Add intermediate markers
        for (var i = 1; i < 10; i++)
        {
            var intermediateMarker = mainMarker + i * intermediateStep;
            if (intermediateMarker < endOffset && intermediateMarker > beginOffset)
            {
                markers.Add(new Marker { Position = intermediateMarker, IsMainMarker = false });
            }
        }
    }

    return markers;
}

private static double FindBeautifulStep(double range, int maxMainMarkers)
{
    double[] beautifulNumbers = { 1, 2, 5, 10 };
    var targetStepSize = range / (maxMainMarkers - 1);

    var exponent = (int)Math.Floor(Math.Log10(targetStepSize));
    var scale = Math.Pow(10, exponent);

    var bestStep = scale;
    var minDifference = double.MaxValue;

    foreach (var factor in beautifulNumbers)
    {
        var currentStep = scale * factor;
        var difference = Math.Abs(currentStep - targetStepSize);
        if (difference < minDifference)
        {
            minDifference = difference;
            bestStep = currentStep;
        }
    }

    return bestStep;
}

    #region Rendering

    public override void Render(DrawingContext context)
    {
        if (DataContext is WaveFormViewModel vm) DrawScale(context, vm);
    }

    private void Redraw()
    {
        _ = Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }

    private Rect _textAreaBounds;

    public Rect TextAreaBounds
    {
        get => _textAreaBounds;
        set
        {
            _textAreaBounds = value;
            Redraw();
        }
    }

    public void DrawScale(DrawingContext context, WaveFormViewModel vm)
    {
        var zoom = vm.ZoomMultiply;
        var multiplier = Wave.CalcMult(vm.Max, Bounds.Width - TextAreaBounds.Width - 12);

        var zm = multiplier / zoom;

        var yOffset = 22;
        var width = Bounds.Width;
        
        var endOffset = vm.Offset + (long)(width * zm);

        var markerX = CalculateMarkers(vm.Offset, endOffset, (int)Bounds.Width / MinScaleNumberWidth);

        //Draw background
        context.FillRectangle(Brushes.Black,
            new Rect(TextAreaBounds.Width, yOffset, Bounds.Width, Bounds.Height));

        if (multiplier == 0) return;
        
        foreach (var mX in markerX)
        {
            var screenXPosition = (mX.Position - vm.Offset) / zm;
            screenXPosition += TextAreaBounds.Width + 1;

            if (screenXPosition < TextAreaBounds.Width || screenXPosition > width) continue;

            if (!mX.IsMainMarker)
            {
                context.DrawLine(_markerBrushPen, new Point(screenXPosition, yOffset - 4), new Point(screenXPosition, yOffset));
            }
            else
            {
                var drawT = TimeHelper.FormatTime(mX.Position, vm.TimeScale, vm.ViewPortWidth);

                //Big marker
                context.DrawLine(_markerBrushPen, new Point(screenXPosition, yOffset - 6), new Point(screenXPosition, Bounds.Height));

                var text = new FormattedText(drawT, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    Typeface.Default,
                    11, this.FindResource(Application.Current?.ActualThemeVariant, "ThemeForegroundBrush") as IBrush);

                //Text
                context.DrawText(text, new Point(screenXPosition - text.Width / 2, 0));
            }
        }
    }

    #endregion
}