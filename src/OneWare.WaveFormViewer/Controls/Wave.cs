using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.Shared.Extensions;
using OneWare.Vcd.Parser.Data;
using OneWare.WaveFormViewer.Enums;
using OneWare.WaveFormViewer.Models;
using ReactiveUI;

namespace OneWare.WaveFormViewer.Controls
{
    public class Wave : Control
    {
        public static readonly StyledProperty<bool> ExtendSignalsProperty =
            AvaloniaProperty.Register<Wave, bool>(nameof(ExtendSignals));

        public bool ExtendSignals
        {
            get => GetValue(ExtendSignalsProperty);
            set => SetValue(ExtendSignalsProperty, value);
        }
        
        public static readonly StyledProperty<long> OffsetProperty =
            AvaloniaProperty.Register<Wave, long>(nameof(Offset));

        public long Offset
        {
            get => GetValue(OffsetProperty);
            set => SetValue(OffsetProperty, value);
        }
        
        public static readonly StyledProperty<long> MaxProperty =
            AvaloniaProperty.Register<Wave, long>(nameof(Max));

        public long Max
        {
            get => GetValue(MaxProperty);
            set => SetValue(MaxProperty, value);
        }
        
        public static readonly StyledProperty<double> ZoomMultiplyProperty =
            AvaloniaProperty.Register<Wave, double>(nameof(ZoomMultiply));

        public double ZoomMultiply
        {
            get => GetValue(ZoomMultiplyProperty);
            set => SetValue(ZoomMultiplyProperty, value);
        }
        
        private readonly Typeface _typeface;
        private readonly IBrush _markerBrush;
        private readonly IPen _markerBrushPen;

        public Wave()
        {
            _markerBrush = (IBrush)new BrushConverter().ConvertFrom("#575151")!;
            _markerBrushPen = new Pen(_markerBrush, 2);

            var fontFamily = Application.Current?.FindResource("EditorFont") as FontFamily;
            _typeface = new Typeface(fontFamily!);
        }

        private CompositeDisposable _compositeDisposable = new();
        protected override void OnDataContextChanged(EventArgs e)
        {
            _compositeDisposable.Dispose();
            _compositeDisposable = new CompositeDisposable();
            if (DataContext is WaveModel model)
            {
                Observable.FromEventPattern<EventArgs>(model.Signal, nameof(model.Signal.RequestRedraw)).Subscribe(x =>
                {
                    Redraw();
                }).DisposeWith(_compositeDisposable);
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if(change.Property == OffsetProperty 
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

            if (mz == 0) return;

            for (var i = 0; i < Bounds.Width;)
            {
                var searchOffset = (long)((i + 2) * mz + Offset);
                var index = model.Signal.FindIndex(searchOffset);
                if(index < 0) break;

                var currentChangeTime = model.Signal.GetChangeTimeFromIndex(index);
                var currentValue = model.Signal.GetValueFromIndex(index);
                var nextChangeTime = model.Signal.GetChangeTimeFromIndex(index + 1);
                //if (nextChangeTime == long.MaxValue) nextChangeTime = Max;
                var x = (currentChangeTime - Offset) / mz;
                var sWidth = (nextChangeTime - currentChangeTime) / mz;

                if (x < 0)
                {
                    sWidth -= 0 - x;
                    x = 0;
                }

                if (sWidth < 1) sWidth++; //sWidth at least 1
                
                if (x > Bounds.Width) break;
                if (sWidth + x > Bounds.Width) sWidth = (int)Bounds.Width - x + 4;

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
                    case (byte)1:
                        startPoint = startPointTop;
                        endPoint = endPointTop;
                        break;
                    case (byte)0:
                        startPoint = startPointBottom;
                        endPoint = endPointBottom;
                        break;
                    case 2:
                        currentPen = zPen;
                        startPoint = startPointMid;
                        endPoint = endPointMid;
                        break;
                    case 3:
                        currentPen = xPen;
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

                if (model.Signal.Type == VcdLineType.Reg) //Simple type
                {
                    if (sWidth > 3)
                    {
                        //Connection
                        if (lastEndPoint.HasValue)
                            context.DrawLine(lastPen, startPoint,
                                (int)lastEndPoint.Value.Y != (int)startPoint.Y
                                    ? new Point(startPoint.X, lastEndPoint.Value.Y)
                                    : lastEndPoint.Value);

                        //Signal
                        switch (currentValue)
                        {
                            case "Z":
                                context.DrawLine(new Pen(Brushes.RoyalBlue, 2), startPoint, endPoint);
                                break;
                            case "X":
                                context.DrawLine(new Pen(Brushes.Red, 2), startPoint, endPoint);
                                break;
                            default:
                                context.DrawLine(signalPen, startPoint, endPoint);
                                break;
                        }
                    }
                    else
                    {
                        context.FillRectangle(model.WaveBrush, new Rect(x, 5, sWidth, Height - 10));
                    }
                }
                else
                {
                    if (sWidth > 20)
                    {
                        DrawByteBorder(context, new Point(startPointTop.X, startPointTop.Y),
                            new Point(endPointTop.X, endPointBottom.Y), signalPen);

                        var cutText = currentValue?.ToString() ?? "";

                        //cutText = SignalConverter.ConvertSignal(cutText, signal.DataType);

                        var maxChars = (int)((sWidth - 4) / (12 * 0.70));
                        if (cutText.Length > maxChars)
                            cutText = maxChars switch
                            {
                                > 1 => cutText[..(maxChars - 1)] + "+",
                                1 => "+",
                                _ => ""
                            };

                        context.DrawText(
                            new FormattedText(cutText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                Typeface.Default, 12, Brushes.White),
                            new Point(startPoint.X + 4, Height / 2 - 7));
                    }
                    else
                    {
                        context.FillRectangle(model.WaveBrush, new Rect(x, 5, sWidth, Height - 10));
                    }
                }

                i += (int)sWidth;
                if (x + sWidth > i) i = (int)(x + sWidth);

                lastEndPoint = endPoint;
                lastPen = currentPen;
            }
        }

        public static double CalcMult(long max, double width)
        {
            return max / (width - 10);
        }

        private void DrawByteBorder(DrawingContext context, Point topLeft, Point bottomRight, IPen pen)
        {
            // Create a collection of points for a polygon  
            var point1 = new Point(topLeft.X + 2, topLeft.Y);
            var point2 = new Point(topLeft.X + 2, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4);
            var point3 = new Point(topLeft.X, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4);
            var point4 = new Point(topLeft.X, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4 * 3);
            var point5 = new Point(topLeft.X + 2, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4 * 3);
            var point6 = new Point(topLeft.X + 2, bottomRight.Y);

            var point7 = new Point(bottomRight.X - 2, bottomRight.Y);
            var point8 = new Point(bottomRight.X - 2, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4 * 3);
            var point9 = new Point(bottomRight.X, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4 * 3);
            var point10 = new Point(bottomRight.X, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4);
            var point11 = new Point(bottomRight.X - 2, topLeft.Y + (bottomRight.Y - topLeft.Y) / 4);
            var point12 = new Point(bottomRight.X - 2, topLeft.Y);
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
}