namespace K617Mod.Core.State;

/// <summary>One point on a response curve. Both values run 0..1.</summary>
public sealed class CurvePoint
{
    public double X { get; set; }
    public double Y { get; set; }

    public CurvePoint() { }

    public CurvePoint(double x, double y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// How far a key is pressed (X, 0..1) translated into how much output
/// the controller reports (Y, 0..1), as a series of points with straight
/// lines between them.
///
/// Deliberately a point list rather than a single exponent. An exponent
/// can only bow the line one way - it cannot express a deadzone (flat,
/// then rising), an S-curve (gentle at both ends, sharp in the middle),
/// or a hard cap. Those are the shapes people actually want when tuning
/// a trigger or a stick by feel, so the extra complexity here buys
/// something real.
///
/// Straight lines between points, not splines: a spline through
/// arbitrary points can overshoot outside 0..1 and can double back on
/// itself, which would mean pressing a key further produced *less*
/// output. Straight segments cannot misbehave that way, and at the
/// resolution a thumb can feel, the difference is invisible.
/// </summary>
public sealed class ResponseCurve
{
    public List<CurvePoint> Points { get; set; } = new();

    public ResponseCurve() { }

    public ResponseCurve(IEnumerable<CurvePoint> points)
    {
        Points = points.ToList();
        Normalize();
    }

    /// <summary>Straight 1:1 - output equals how far the key is pressed.</summary>
    public static ResponseCurve Linear() => new(new[]
    {
        new CurvePoint(0, 0),
        new CurvePoint(1, 1),
    });

    /// <summary>
    /// Samples y = x^exponent into a point list. Used to carry the old
    /// exponent-based tuning across to the new format without changing
    /// how anything felt: exponent 2.0 produces the same softened
    /// trigger response the constants used to give.
    /// </summary>
    public static ResponseCurve FromExponent(double exponent, int segments = 4)
    {
        if (segments < 1) segments = 1;

        var points = new List<CurvePoint>(segments + 1);
        for (var i = 0; i <= segments; i++)
        {
            var x = (double)i / segments;
            points.Add(new CurvePoint(x, Math.Pow(x, exponent)));
        }

        return new ResponseCurve(points);
    }

    /// <summary>
    /// Output for a given press depth. Input outside 0..1 is clamped, so
    /// a miscalibrated depth reading can never produce out-of-range
    /// controller output.
    /// </summary>
    public double Evaluate(double input)
    {
        if (Points.Count == 0) return Math.Clamp(input, 0.0, 1.0);

        var x = Math.Clamp(input, 0.0, 1.0);

        if (Points.Count == 1) return Math.Clamp(Points[0].Y, 0.0, 1.0);

        if (x <= Points[0].X) return Points[0].Y;
        if (x >= Points[^1].X) return Points[^1].Y;

        for (var i = 0; i < Points.Count - 1; i++)
        {
            var a = Points[i];
            var b = Points[i + 1];
            if (x > b.X) continue;

            var span = b.X - a.X;
            if (span <= 0) return b.Y; // duplicate X - take the later point rather than dividing by zero

            var t = (x - a.X) / span;
            return a.Y + t * (b.Y - a.Y);
        }

        return Points[^1].Y;
    }

    /// <summary>
    /// Sorts by X, clamps everything into 0..1, and guarantees a point
    /// at each end. Called after construction and after any edit, so
    /// Evaluate can assume a well-formed list rather than re-checking on
    /// every single tick.
    /// </summary>
    public void Normalize()
    {
        foreach (var point in Points)
        {
            point.X = Math.Clamp(point.X, 0.0, 1.0);
            point.Y = Math.Clamp(point.Y, 0.0, 1.0);
        }

        Points = Points.OrderBy(p => p.X).ToList();

        if (Points.Count == 0)
        {
            Points.Add(new CurvePoint(0, 0));
            Points.Add(new CurvePoint(1, 1));
            return;
        }

        if (Points[0].X > 0) Points.Insert(0, new CurvePoint(0, Points[0].Y));
        if (Points[^1].X < 1) Points.Add(new CurvePoint(1, Points[^1].Y));
    }

    public ResponseCurve Clone() =>
        new(Points.Select(p => new CurvePoint(p.X, p.Y)));
}
