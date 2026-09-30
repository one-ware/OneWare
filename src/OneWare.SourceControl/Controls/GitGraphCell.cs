using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using OneWare.SourceControl.Models;

namespace OneWare.SourceControl.Controls;

/// <summary>
///     Draws the lanes, curves and commit node of one <see cref="GitGraphRow" />.
///     With <see cref="IsContinuation" /> only the lanes that continue below the commit are drawn,
///     which keeps the graph connected through the expanded file list.
/// </summary>
public class GitGraphCell : Control
{
    public const double LaneWidth = 14;
    private const double NodeRadius = 4;
    private const double LineThickness = 1.6;

    public static readonly StyledProperty<GitGraphRow?> RowProperty =
        AvaloniaProperty.Register<GitGraphCell, GitGraphRow?>(nameof(Row));

    public static readonly StyledProperty<bool> IsContinuationProperty =
        AvaloniaProperty.Register<GitGraphCell, bool>(nameof(IsContinuation));

    public static readonly StyledProperty<IBrush?> NodeBackgroundProperty =
        AvaloniaProperty.Register<GitGraphCell, IBrush?>(nameof(NodeBackground));

    private static readonly Color[] Palette =
    [
        Color.Parse("#3794FF"),
        Color.Parse("#FFB000"),
        Color.Parse("#DC267F"),
        Color.Parse("#40B0A6"),
        Color.Parse("#B66DFF"),
        Color.Parse("#E5704B"),
        Color.Parse("#6CC24A"),
        Color.Parse("#C9A227")
    ];

    private static readonly IBrush[] LaneBrushes =
        Palette.Select(x => (IBrush)new ImmutableSolidColorBrush(x)).ToArray();

    private static readonly IPen[] LanePens = Palette
        .Select(x => (IPen)new ImmutablePen(new ImmutableSolidColorBrush(x), LineThickness, lineCap: PenLineCap.Round))
        .ToArray();

    private static readonly IPen[] NodePens = Palette
        .Select(x => (IPen)new ImmutablePen(new ImmutableSolidColorBrush(x), 2)).ToArray();

    static GitGraphCell()
    {
        AffectsMeasure<GitGraphCell>(RowProperty);
        AffectsRender<GitGraphCell>(RowProperty, IsContinuationProperty, NodeBackgroundProperty);
    }

    public GitGraphRow? Row
    {
        get => GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    public bool IsContinuation
    {
        get => GetValue(IsContinuationProperty);
        set => SetValue(IsContinuationProperty, value);
    }

    public IBrush? NodeBackground
    {
        get => GetValue(NodeBackgroundProperty);
        set => SetValue(NodeBackgroundProperty, value);
    }

    public static IBrush GetLaneBrush(int color) => LaneBrushes[Math.Abs(color) % LaneBrushes.Length];

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(Math.Max(1, Row?.LaneCount ?? 0) * LaneWidth + 2, 0);
    }

    public override void Render(DrawingContext context)
    {
        if (Row is not { } row) return;

        var height = Bounds.Height;

        if (IsContinuation)
        {
            foreach (var lane in row.Continuation)
                context.DrawLine(GetPen(lane.Color), new Point(X(lane.Lane), 0), new Point(X(lane.Lane), height));
            return;
        }

        var center = Math.Round(height / 2);
        var node = new Point(X(row.Lane), center);

        foreach (var line in row.Lines)
        {
            var pen = GetPen(line.Color);
            switch (line.Kind)
            {
                case GitGraphLineKind.PassThrough:
                    context.DrawLine(pen, new Point(X(line.From), 0), new Point(X(line.From), height));
                    break;
                case GitGraphLineKind.Incoming when line.From == row.Lane:
                    context.DrawLine(pen, new Point(node.X, 0), node);
                    break;
                case GitGraphLineKind.Incoming:
                    context.DrawGeometry(null, pen, Curve(new Point(X(line.From), 0), node, true));
                    break;
                case GitGraphLineKind.Outgoing when line.To == row.Lane:
                    context.DrawLine(pen, node, new Point(node.X, height));
                    break;
                case GitGraphLineKind.Outgoing:
                    context.DrawGeometry(null, pen, Curve(node, new Point(X(line.To), height), false));
                    break;
            }
        }

        var brush = GetLaneBrush(row.Color);
        var background = NodeBackground ?? Brushes.Transparent;
        var nodePen = NodePens[Math.Abs(row.Color) % NodePens.Length];

        if (row.IsHead)
        {
            context.DrawEllipse(background, nodePen, node, NodeRadius + 1, NodeRadius + 1);
            context.DrawEllipse(brush, null, node, NodeRadius - 1.5, NodeRadius - 1.5);
        }
        else if (row.IsMerge)
        {
            context.DrawEllipse(background, nodePen, node, NodeRadius - 0.5, NodeRadius - 0.5);
        }
        else
        {
            context.DrawEllipse(brush, null, node, NodeRadius, NodeRadius);
        }
    }

    private static IPen GetPen(int color) => LanePens[Math.Abs(color) % LanePens.Length];

    private static double X(int lane) => lane * LaneWidth + LaneWidth / 2 + 1;

    /// <summary>
    ///     Rounded corner between a vertical lane and the node row, either entering the node from above
    ///     or leaving it towards another lane below.
    /// </summary>
    private static StreamGeometry Curve(Point from, Point to, bool verticalFirst)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(from, false);
        var direction = Math.Sign(to.X - from.X);
        var radius = Math.Min(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));

        if (verticalFirst)
        {
            ctx.LineTo(new Point(from.X, to.Y - radius));
            ctx.QuadraticBezierTo(new Point(from.X, to.Y), new Point(from.X + direction * radius, to.Y));
        }
        else
        {
            ctx.LineTo(new Point(to.X - direction * radius, from.Y));
            ctx.QuadraticBezierTo(new Point(to.X, from.Y), new Point(to.X, from.Y + radius));
        }

        ctx.LineTo(to);
        ctx.EndFigure(false);
        return geometry;
    }
}
