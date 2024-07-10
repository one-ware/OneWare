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

namespace OneWare.WaveFormViewer.Controls
{
    public class WaveFormScale : Control
    {
        private readonly IPen _markerBrushPen;
        
        public WaveFormScale()
        {
            ClipToBounds = true;
            var markerBrush = (IBrush)new BrushConverter().ConvertFrom("#575151")!;
            _markerBrushPen = new Pen(markerBrush, 2);
        }

        private CompositeDisposable _disposableReg = new();
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            _disposableReg.Dispose();
            _disposableReg = new CompositeDisposable();
            
            if (DataContext is WaveFormViewModel vm)
            {
                vm.WhenValueChanged(x => x.Max).Subscribe(x =>
                {
                    Redraw();
                }).DisposeWith(_disposableReg);
                vm.WhenValueChanged(x => x.Offset).Subscribe(x =>
                {
                    Redraw();
                }).DisposeWith(_disposableReg);
                vm.WhenValueChanged(x => x.ZoomMultiply).Subscribe(x =>
                {
                    Redraw();
                }).DisposeWith(_disposableReg);
                vm.WhenValueChanged(x => x.TimeScale).Subscribe(x =>
                {
                    Redraw();
                }).DisposeWith(_disposableReg);
            }
        }

        /// <summary>
        ///     Returns List of markers relevant for rendering (absolute values in FS!)
        /// </summary>
        private static List<long> CalculateMarkers(long offset, double multiplier, double zoom, double width, long max)
        {
            var min = offset;
            var dist = (long)(width * multiplier / zoom);

            var maxR = FloorZero(max);

            var distR = (long)(maxR / (4 * zoom));

            var n = new List<long>();

            if (distR == 0) return n;

            long minR = 0;
            for (long i = 0; i <= min; i += distR) minR = i;

            for (var i = minR; i <= min + dist + distR; i += distR) n.Add(i);

            return n;
        }

        private static long FloorZero(long number)
        {
            if (number == 0) return 0;
            var pow = (int)Math.Log10(number);
            var factor = (long)BigInteger.Pow(10, pow);
            var temp = number / factor;
            return (long)(temp * factor);
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

            var markerX = CalculateMarkers(vm.Offset, multiplier, zoom, width - TextAreaBounds.Width, vm.Max);

            //Draw background
            context.FillRectangle(Brushes.Black,
                new Rect(TextAreaBounds.Width, yOffset, Bounds.Width, Bounds.Height));

            if (multiplier == 0) return;

            double lastXx = -1;
            foreach (var mX in markerX)
            {
                var xxx = (mX - vm.Offset) / zm;
                var xx = xxx + TextAreaBounds.Width + 1;

                //distance between marker one and two
                var dist = xx - lastXx;
                var mDist = dist / 5;

                if (mDist > 0 && lastXx > -1) //Small markers
                    for (var x = lastXx+mDist; x < xx; x += mDist)
                    {
                        if (x < TextAreaBounds.Width) continue;
                        if (x > width) break;
                        context.DrawLine(_markerBrushPen, new Point(x, yOffset-4), new Point(x, yOffset));
                    }

                lastXx = xx;
                if (xx < TextAreaBounds.Width || xx > width) continue;

                var drawT = TimeHelper.ConvertNumber(mX, vm.TimeScale);

                //Big marker
                context.DrawLine(_markerBrushPen, new Point(xx, yOffset-6), new Point(xx, Bounds.Height));

                var text = new FormattedText(drawT, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        Typeface.Default,
                        11, this.FindResource(Application.Current?.ActualThemeVariant, "ThemeForegroundBrush") as IBrush);
                
                //Text
                context.DrawText(text, new Point(xx - text.Width / 2, 0));
            }
        }

        #endregion
    }
}