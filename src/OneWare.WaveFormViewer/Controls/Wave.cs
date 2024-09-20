using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Vcd.Parser.Data;
using OneWare.WaveFormViewer.Models;

namespace OneWare.WaveFormViewer.Controls;

public class Wave : Control
{
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<Wave>();

    public static readonly StyledProperty<bool> ExtendSignalsProperty =
        AvaloniaProperty.Register<Wave, bool>(nameof(ExtendSignals));

    public static readonly StyledProperty<long> LoadingOffsetProperty =
        AvaloniaProperty.Register<Wave, long>(nameof(LoadingOffset));

    public static readonly StyledProperty<long> OffsetProperty =
        AvaloniaProperty.Register<Wave, long>(nameof(Offset));

    public static readonly StyledProperty<long> MaxProperty =
        AvaloniaProperty.Register<Wave, long>(nameof(Max));

    public static readonly StyledProperty<double> ZoomMultiplyProperty =
        AvaloniaProperty.Register<Wave, double>(nameof(ZoomMultiply));

    private readonly IBrush _markerBrush;
    private readonly IPen _markerBrushPen;

    private readonly Typeface _typeface;

    private CompositeDisposable _compositeDisposable = new();

    public Wave()
    {
        _markerBrush = (IBrush)new BrushConverter().ConvertFrom("#575151")!;
        _markerBrushPen = new Pen(_markerBrush, 2);

        var fontFamily = Application.Current?.FindResource("EditorFont") as FontFamily;
        _typeface = new Typeface(fontFamily!);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public bool ExtendSignals
    {
        get => GetValue(ExtendSignalsProperty);
        set => SetValue(ExtendSignalsProperty, value);
    }

    public long LoadingOffset
    {
        get => GetValue(LoadingOffsetProperty);
        set => SetValue(LoadingOffsetProperty, value);
    }

    public long Offset
    {
        get => GetValue(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    public long Max
    {
        get => GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public double ZoomMultiply
    {
        get => GetValue(ZoomMultiplyProperty);
        set => SetValue(ZoomMultiplyProperty, value);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        _compositeDisposable.Dispose();
        _compositeDisposable = new CompositeDisposable();
        if (DataContext is WaveModel model)
        {
            Observable.FromEventPattern<EventArgs>(model.Signal, nameof(model.Signal.RequestRedraw))
                .Subscribe(x => { Redraw(); }).DisposeWith(_compositeDisposable);

            model.WhenValueChanged(x => x.DataType).Subscribe(x => { Redraw(); }).DisposeWith(_compositeDisposable);

            model.WhenValueChanged(x => x.FixedPointShift).Subscribe(x => { Redraw(); })
                .DisposeWith(_compositeDisposable);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == OffsetProperty
            || change.Property == ZoomMultiplyProperty
            || change.Property == MaxProperty)
            Redraw();

        base.OnPropertyChanged(change);
    }

    #region Rendering

    public override void Render(DrawingContext context)
    {
        if (DataContext is WaveModel model)
            DrawSignal(context, model);
    }

    public void Redraw()
    {
        _ = Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }

    public void DrawSignal(DrawingContext context, WaveModel model)
    {
        var typeface = new Typeface(FontFamily);

        IPen signalPen = new Pen(model.WaveBrush, 2);
        IPen xPen = new Pen(Brushes.Red, 2);
        IPen zPen = new Pen(Brushes.RoyalBlue, 2);

        context.DrawLine(new Pen(_markerBrush), new Point(1, Height),
            new Point(Bounds.Width, Height));

        var zoom = ZoomMultiply;
        var multiplier = CalcMult(Max, Bounds.Width);
        var mz = multiplier / zoom;

        Point? lastEndPoint = null;
        var lastPen = signalPen;
        object? lastValue = null;
        int lastIndex = -1;

        if (mz == 0) return;

        for (var i = 0d; i < Bounds.Width;)
        {
            var searchOffset = (long)((i + 2) * mz + Offset);
            var index = model.Signal.FindIndex(searchOffset);

            var currentChangeTime = index < 0 ? 0 : model.Signal.GetChangeTimeFromIndex(index);
            var currentValue = index < 0 ? StdLogic.U : model.Signal.GetValueFromIndex(index);
            var nextChangeTime = index < 0
                ? model.Signal.GetChangeTimeFromIndex(0)
                : model.Signal.GetChangeTimeFromIndex(index + 1);

            if (LoadingOffset > 0)
                if (nextChangeTime > LoadingOffset)
                    nextChangeTime = LoadingOffset;
            //if(nextChangeTime >= currentChangeTime) break;
            var x = (currentChangeTime - Offset) / mz;
            var sWidth = (nextChangeTime - currentChangeTime) / mz;

            if (x < 0)
            {
                sWidth -= 0 - x;
                x = 0;
            }

            if (sWidth < 1) sWidth = 1;

            if (x > Bounds.Width) break;
            if (sWidth + x > Bounds.Width) sWidth = (int)Bounds.Width - x + 6;

            var startPointTop = new Point(x, 5);
            var startPointMid = new Point(x, Height / 2);
            var startPointBottom = new Point(x, Height - 5);
            var endPointTop = new Point(startPointTop.X + sWidth, startPointTop.Y);
            var endPointMid = new Point(startPointMid.X + sWidth, startPointMid.Y);
            var endPointBottom = new Point(startPointMid.X + sWidth, startPointBottom.Y);

            Point startPoint;
            Point endPoint;
            var currentPen = signalPen;

            switch (currentValue)
            {
                case StdLogic.Full:
                    startPoint = startPointTop;
                    endPoint = endPointTop;
                    break;
                case StdLogic.Zero:
                    startPoint = startPointBottom;
                    endPoint = endPointBottom;
                    break;
                case StdLogic.U:
                    currentPen = xPen;
                    startPoint = startPointMid;
                    endPoint = endPointMid;
                    break;
                case StdLogic.X:
                    currentPen = xPen;
                    startPoint = startPointMid;
                    endPoint = endPointMid;
                    break;
                case StdLogic.Z:
                    currentPen = zPen;
                    startPoint = startPointMid;
                    endPoint = endPointMid;
                    break;
                case StdLogic[] stdLogicArray:
                    if (stdLogicArray.Contains(StdLogic.U))
                        currentPen = xPen;
                    else if (stdLogicArray.Contains(StdLogic.Z))
                        currentPen = zPen;
                    startPoint = startPointMid;
                    endPoint = endPointMid;
                    break;
                default:
                {
                    startPoint = startPointMid;
                    endPoint = endPointMid;
                    break;
                }
            }

            if (model.Signal is { Type: VcdLineType.Reg or VcdLineType.Wire, BitWidth: <= 1 }) //Simple type
            {
                //Can happen if resolution is too small to display very short changes (eg from 0 to 1 to 0 in short timeframe)
                //We want to draw a change there
                if (lastIndex >= 0)
                {
                    for (var ic = index; ic >= lastIndex; ic--)
                    {
                        if (!currentValue!.Equals(model.Signal.GetValueFromIndex(ic)))
                        {
                            context.DrawLine(currentPen, startPointBottom, startPointTop);
                            break;
                        }
                    }
                }
                if (sWidth > 1)
                {
                    //Connection
                    if (lastEndPoint.HasValue)
                        context.DrawLine(lastPen, startPoint,
                            (int)lastEndPoint.Value.Y != (int)startPoint.Y
                                ? new Point(startPoint.X, lastEndPoint.Value.Y)
                                : lastEndPoint.Value);

                    context.DrawLine(currentPen, startPoint, endPoint);
                }
                else
                {
                    context.DrawLine(currentPen, startPointBottom, startPointTop);
                }
            }
            else
            {
                if (sWidth > 20)
                {
                    if (Offset > currentChangeTime)
                        DrawByteBorder(context, new Point(startPointTop.X - 10, startPointTop.Y),
                            new Point(endPointTop.X, endPointBottom.Y), currentPen);
                    else
                        DrawByteBorder(context, new Point(startPointTop.X, startPointTop.Y),
                            new Point(endPointTop.X, endPointBottom.Y), currentPen);


                    const int fontSize = 12;

                    var cutText = SignalConverter.ConvertSignal(currentValue ?? 0, model);

                    var text = new FormattedText("-", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        typeface, fontSize, Brushes.Transparent);

                    var maxChars = (int)((sWidth - 6) / text.Width);

                    if (cutText.Length > maxChars)
                        cutText = maxChars switch
                        {
                            > 1 => cutText[..(maxChars - 1)] + "+",
                            1 => "+",
                            _ => ""
                        };

                    context.DrawText(
                        new FormattedText(cutText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface,
                            fontSize, Brushes.White),
                        new Point(startPoint.X + 4, Height / 2 - 7));
                }
                else
                {
                    context.FillRectangle(currentPen.Brush!, new Rect(x, 5, sWidth, Height - 10));
                }
            }

            if (nextChangeTime == LoadingOffset) break;

            i += sWidth;

            lastEndPoint = endPoint;
            lastPen = currentPen;
            lastValue = currentValue;
            lastIndex = index;
        }
    }

    public static double CalcMult(long max, double width)
    {
        return max / (width - 10);
    }

    private void DrawByteBorder(DrawingContext context, Point topLeft, Point bottomRight, IPen pen)
    {
        // Create a collection of points for a polygon  
        var point1 = new Point(topLeft.X + 3, topLeft.Y);
        var point2 = new Point(topLeft.X + 3, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4);
        var point3 = new Point(topLeft.X + 1, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4);
        var point4 = new Point(topLeft.X + 1, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4 * 3);
        var point5 = new Point(topLeft.X + 3, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4 * 3);
        var point6 = new Point(topLeft.X + 3, bottomRight.Y);

        var point7 = new Point(bottomRight.X - 3, bottomRight.Y);
        var point8 = new Point(bottomRight.X - 3, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4 * 3);
        var point9 = new Point(bottomRight.X - 1, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4 * 3);
        var point10 = new Point(bottomRight.X - 1, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4);
        var point11 = new Point(bottomRight.X - 3, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4);
        var point12 = new Point(bottomRight.X - 3, topLeft.Y);
        IList<Point> polygonPoints = new List<Point>
        {
            point1,
            point2,
            point3,
            point4,
            point5,
            point6,
            point7,
            point8,
            point9,
            point10,
            point11,
            point12
        };
        // Draw polygon
        for (var i = 0; i < polygonPoints.Count - 1; i++)
            context.DrawLine(pen, polygonPoints[i], polygonPoints[i + 1]);
        context.DrawLine(pen, polygonPoints[^1], polygonPoints[0]);
    }

    #endregion
}