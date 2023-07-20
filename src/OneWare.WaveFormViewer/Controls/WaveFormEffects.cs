using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.WaveFormViewer.ViewModels;

namespace OneWare.WaveFormViewer.Controls
{
    public class WaveFormEffects : Control
    {
        private readonly IPen _markerBrushPen;

        public WaveFormEffects()
        {
            ClipToBounds = true;
            IBrush markerBrush = Brushes.DarkRed;
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
            }
        }

        #region Rendering

        public override void Render(DrawingContext context)
        {
            if (DataContext is not WaveFormViewModel vm) return;
            var multiplier = Wave.CalcMult(vm.Max, Bounds.Width);

            if (vm.MarkerOffset != long.MaxValue)
            {
                var xxx = (vm.MarkerOffset - vm.Offset) / (multiplier / vm.ZoomMultiply);

                if (xxx > 0 && xxx < Bounds.Width)
                    context.DrawLine(_markerBrushPen, new Point(xxx, 0), new Point(xxx, Bounds.Height));
            }

            if (vm.SecondMarkerOffset != long.MaxValue)
            {
                var xxx = (vm.SecondMarkerOffset - vm.Offset) / (multiplier / vm.ZoomMultiply);

                if (xxx > 0 && xxx < Bounds.Width)
                    context.DrawLine(_markerBrushPen, new Point(xxx, 0), new Point(xxx, Bounds.Height));
            }
        }

        public void SetPos(double x, bool pointerPressed, bool cursorOnly = false)
        {
            if (DataContext is not WaveFormViewModel vm) return;

            var multiplier = Wave.CalcMult(vm.Max, Bounds.Width);
            var offset = (long)((vm.Offset * vm.ZoomMultiply + x * multiplier) / vm.ZoomMultiply);
            vm.CursorOffset = offset;

            if (cursorOnly) return;

            if (!pointerPressed)
            {
                vm.SecondMarkerOffset = long.MaxValue;
                vm.MarkerOffset = offset;
            }
            else
            {
                vm.SecondMarkerOffset = offset;
            }

            Redraw();
        }

        private void Redraw()
        {
            _ = Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        }

        #endregion*/
    }
}