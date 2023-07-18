using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using OneWare.WaveFormViewer.Models;
using ReactiveUI;

namespace OneWare.WaveFormViewer.Controls
{
    public class Wave : Control
    {
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
            if (DataContext is not WaveModel signal) return;
            IPen signalPen = new Pen(model.WaveBrush, 2);
            IPen xPen = new Pen(Brushes.Red, 2);
            IPen zPen = new Pen(Brushes.RoyalBlue, 2);

            context.DrawLine(new Pen(_markerBrush), new Point(1, Height),
                new Point(Bounds.Width, Height));
        }

        private static (WavePart, WavePart)? SearchSignal(WavePart[] signalLine, long offset, bool clk)
        {
            if (signalLine.Length <= 1) return null;
            if (clk)
            {
                var max = signalLine[^1].Time;
                var newOffset = offset % max;
                var diff = offset - newOffset;
                if (diff < 0) diff = 0;

                var l = SearchSignalIndex(signalLine, newOffset);

                if (l < 0 || l + 1 >= signalLine.Length) return null;
                return (signalLine[l].AddTime(diff), signalLine[l + 1].AddTime(diff));
            }
            else
            {
                var l = SearchSignalIndex(signalLine, offset);

                if (l < 0 || l + 1 >= signalLine.Length) return null;
                return (signalLine[l], signalLine[l + 1]);
            }
        }

        private static int SearchSignalIndex(WavePart[] signalLine, long x)
        {
            var l = BinarySearchIterative(signalLine, x);

            if (l > -1 && x >= signalLine[l].Time) return l;

            return -1;
        }


        private static int BinarySearchIterative(WavePart[] inputArray, long key)
        {
            var min = 0;
            var max = inputArray.Length - 1;
            while (min <= max)
            {
                var mid = (min + max) / 2;
                if (key == inputArray[mid].Time) return mid;
                if (key < inputArray[mid].Time)
                    max = mid - 1;
                else
                    min = mid + 1;
            }

            return min - 1;
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