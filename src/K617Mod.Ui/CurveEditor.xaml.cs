using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using K617Mod.Core.State;

namespace K617Mod.Ui;

/// <summary>
/// Draggable response-curve graph. Press depth runs left to right,
/// controller output runs bottom to top.
///
/// Drag a handle to move it. Double-click empty space to add a point.
/// Right-click a handle to remove it. The two end handles can move
/// vertically but not horizontally - a curve has to say what happens at
/// no press and at full press, so those two points always exist.
///
/// The canvas is redrawn wholesale on every change rather than mutating
/// individual shapes. At a couple of dozen shapes that is cheap, and it
/// removes a whole class of bug where the drawing and the point list
/// drift apart.
/// </summary>
public partial class CurveEditor : UserControl
{
    // Plot area inside the canvas, leaving room for axis labels.
    private const double Left = 58;
    private const double Top = 22;
    private const double PlotWidth = 382;
    private const double PlotHeight = 332;
    private const double Bottom = Top + PlotHeight;
    private const double Right = Left + PlotWidth;

    private const double HandleRadius = 6.5;
    private const double HitRadius = 11;

    private List<CurvePoint> _points = ResponseCurve.Linear().Points;
    private int _dragIndex = -1;

    public CurveEditor()
    {
        InitializeComponent();
        Redraw();
    }

    /// <summary>Raised whenever the person changes the curve. Not raised by LoadCurve.</summary>
    public event EventHandler? CurveChanged;

    private bool _isReadOnly;

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set { _isReadOnly = value; _dragIndex = -1; Redraw(); }
    }

    /// <summary>Replaces the displayed curve without raising CurveChanged.</summary>
    public void LoadCurve(ResponseCurve curve)
    {
        _points = curve.Clone().Points;
        _dragIndex = -1;
        Redraw();
    }

    public ResponseCurve BuildCurve() =>
        new(_points.Select(p => new CurvePoint(p.X, p.Y)));

    // ---------- coordinate conversion ----------

    private static double ToPlotX(double x) => Left + x * PlotWidth;
    private static double ToPlotY(double y) => Bottom - y * PlotHeight;
    private static double FromPlotX(double px) => Math.Clamp((px - Left) / PlotWidth, 0.0, 1.0);
    private static double FromPlotY(double py) => Math.Clamp((Bottom - py) / PlotHeight, 0.0, 1.0);

    // ---------- input ----------

    private void Plot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsReadOnly) return;

        var position = e.GetPosition(Plot);
        var hit = FindHandleAt(position);

        if (hit >= 0)
        {
            _dragIndex = hit;
            Plot.CaptureMouse();
            return;
        }

        // Double-clicking empty space adds a point there. Single clicks
        // do nothing, so a stray click while aiming at a handle can't
        // silently litter the curve with points.
        if (e.ClickCount == 2 && IsInsidePlot(position))
        {
            var newPoint = new CurvePoint(FromPlotX(position.X), FromPlotY(position.Y));
            _points.Add(newPoint);
            _points = _points.OrderBy(p => p.X).ToList();
            Redraw();
            CurveChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Plot_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragIndex < 0 || IsReadOnly) return;

        var position = e.GetPosition(Plot);
        var point = _points[_dragIndex];

        point.Y = FromPlotY(position.Y);

        // End points are pinned horizontally; middle ones can slide but
        // never past a neighbour, which would flip the curve back on
        // itself and make "press harder, get less" possible.
        var isFirst = _dragIndex == 0;
        var isLast = _dragIndex == _points.Count - 1;

        if (!isFirst && !isLast)
        {
            const double gap = 0.005;
            var lower = _points[_dragIndex - 1].X + gap;
            var upper = _points[_dragIndex + 1].X - gap;
            point.X = upper <= lower ? lower : Math.Clamp(FromPlotX(position.X), lower, upper);
        }

        Redraw();
        CurveChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Plot_MouseLeftButtonUp(object sender, MouseEventArgs e)
    {
        if (_dragIndex < 0) return;
        _dragIndex = -1;
        Plot.ReleaseMouseCapture();
    }

    /// <summary>Right-clicking a handle removes it, except the two ends.</summary>
    private void Handle_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsReadOnly) return;
        if (sender is not Ellipse handle || handle.Tag is not int index) return;
        if (index == 0 || index == _points.Count - 1) return;

        _points.RemoveAt(index);
        Redraw();
        CurveChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private int FindHandleAt(Point position)
    {
        for (var i = 0; i < _points.Count; i++)
        {
            var dx = ToPlotX(_points[i].X) - position.X;
            var dy = ToPlotY(_points[i].Y) - position.Y;
            if (dx * dx + dy * dy <= HitRadius * HitRadius) return i;
        }
        return -1;
    }

    private static bool IsInsidePlot(Point p) =>
        p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;

    // ---------- drawing ----------

    private void Redraw()
    {
        Plot.Children.Clear();

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x30, 0x35));
        var axisBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x61, 0x6A));
        var labelBrush = new SolidColorBrush(Color.FromRgb(0x7A, 0x82, 0x8C));
        var curveBrush = new SolidColorBrush(IsReadOnly
            ? Color.FromRgb(0x6E, 0x76, 0x81)
            : Color.FromRgb(0x4E, 0xA1, 0xF0));

        // Grid every 25%
        for (var step = 0; step <= 4; step++)
        {
            var fraction = step / 4.0;

            Plot.Children.Add(new Line
            {
                X1 = ToPlotX(fraction), Y1 = Top,
                X2 = ToPlotX(fraction), Y2 = Bottom,
                Stroke = gridBrush, StrokeThickness = 1,
            });

            Plot.Children.Add(new Line
            {
                X1 = Left, Y1 = ToPlotY(fraction),
                X2 = Right, Y2 = ToPlotY(fraction),
                Stroke = gridBrush, StrokeThickness = 1,
            });

            var xLabel = new TextBlock
            {
                Text = $"{fraction * 100:0}%",
                Foreground = labelBrush,
                FontSize = 10,
            };
            Canvas.SetLeft(xLabel, ToPlotX(fraction) - 12);
            Canvas.SetTop(xLabel, Bottom + 6);
            Plot.Children.Add(xLabel);

            var yLabel = new TextBlock
            {
                Text = $"{fraction * 100:0}%",
                Foreground = labelBrush,
                FontSize = 10,
            };
            Canvas.SetLeft(yLabel, Left - 30);
            Canvas.SetTop(yLabel, ToPlotY(fraction) - 7);
            Plot.Children.Add(yLabel);
        }

        // Reference line showing what a straight 1:1 response looks like,
        // so how far the curve departs from neutral is readable at a glance.
        Plot.Children.Add(new Line
        {
            X1 = ToPlotX(0), Y1 = ToPlotY(0),
            X2 = ToPlotX(1), Y2 = ToPlotY(1),
            Stroke = axisBrush,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 },
        });

        // Axes
        Plot.Children.Add(new Line
        {
            X1 = Left, Y1 = Bottom, X2 = Right, Y2 = Bottom,
            Stroke = axisBrush, StrokeThickness = 1.5,
        });
        Plot.Children.Add(new Line
        {
            X1 = Left, Y1 = Top, X2 = Left, Y2 = Bottom,
            Stroke = axisBrush, StrokeThickness = 1.5,
        });

        var xTitle = new TextBlock
        {
            Text = "Key press depth",
            Foreground = labelBrush,
            FontSize = 11,
        };
        Canvas.SetLeft(xTitle, Left + PlotWidth / 2 - 40);
        Canvas.SetTop(xTitle, Bottom + 22);
        Plot.Children.Add(xTitle);

        // Rotating -90 about the element's own origin sends the text
        // upward and leftward from wherever it is placed, so this sits
        // hard against the canvas edge - clear of the percentage labels,
        // which start at Left - 30.
        var yTitle = new TextBlock
        {
            Text = "Controller output",
            Foreground = labelBrush,
            FontSize = 11,
            RenderTransform = new RotateTransform(-90),
            RenderTransformOrigin = new Point(0, 0),
        };
        Canvas.SetLeft(yTitle, 2);
        Canvas.SetTop(yTitle, Top + PlotHeight / 2 + 48);
        Plot.Children.Add(yTitle);

        // The curve. Straight segments between points, so a polyline
        // through the points *is* the curve - nothing to sample.
        var polyline = new Polyline
        {
            Stroke = curveBrush,
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round,
            Points = new PointCollection(
                _points.Select(p => new Point(ToPlotX(p.X), ToPlotY(p.Y)))),
        };
        Plot.Children.Add(polyline);

        // Handles
        for (var i = 0; i < _points.Count; i++)
        {
            var isEnd = i == 0 || i == _points.Count - 1;

            var handle = new Ellipse
            {
                Width = HandleRadius * 2,
                Height = HandleRadius * 2,
                Fill = new SolidColorBrush(IsReadOnly
                    ? Color.FromRgb(0x44, 0x4A, 0x52)
                    : isEnd ? Color.FromRgb(0x2A, 0x2D, 0x31) : Color.FromRgb(0x4E, 0xA1, 0xF0)),
                Stroke = curveBrush,
                StrokeThickness = 2,
                Tag = i,
                Cursor = IsReadOnly ? Cursors.Arrow : Cursors.Hand,
                ToolTip = $"{_points[i].X * 100:0}% depth → {_points[i].Y * 100:0}% output"
                          + (isEnd ? " (end point, moves vertically only)" : "  ·  right-click to remove"),
            };

            handle.MouseRightButtonDown += Handle_MouseRightButtonDown;

            Canvas.SetLeft(handle, ToPlotX(_points[i].X) - HandleRadius);
            Canvas.SetTop(handle, ToPlotY(_points[i].Y) - HandleRadius);
            Plot.Children.Add(handle);
        }
    }
}
