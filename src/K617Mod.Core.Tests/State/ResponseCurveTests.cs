using K617Mod.Core.State;
using Xunit;

namespace K617Mod.Core.Tests.State;

public class ResponseCurveTests
{
    [Fact]
    public void Linear_PassesInputStraightThrough()
    {
        var curve = ResponseCurve.Linear();

        Assert.Equal(0.0, curve.Evaluate(0.0), 6);
        Assert.Equal(0.25, curve.Evaluate(0.25), 6);
        Assert.Equal(0.5, curve.Evaluate(0.5), 6);
        Assert.Equal(1.0, curve.Evaluate(1.0), 6);
    }

    [Fact]
    public void FromExponent_MatchesTheOldSquaredResponseAtItsSamplePoints()
    {
        // The behaviour the fixed ThrottleBrakeCurveExponent = 2.0 gave:
        // half depth produced a quarter output.
        var curve = ResponseCurve.FromExponent(2.0);

        Assert.Equal(0.25, curve.Evaluate(0.5), 6);
        Assert.Equal(0.0, curve.Evaluate(0.0), 6);
        Assert.Equal(1.0, curve.Evaluate(1.0), 6);
    }

    [Fact]
    public void Evaluate_InterpolatesBetweenPoints()
    {
        var curve = new ResponseCurve(new[]
        {
            new CurvePoint(0.0, 0.0),
            new CurvePoint(0.5, 0.8),
            new CurvePoint(1.0, 1.0),
        });

        Assert.Equal(0.4, curve.Evaluate(0.25), 6);  // halfway along the first segment
        Assert.Equal(0.9, curve.Evaluate(0.75), 6);  // halfway along the second
    }

    [Fact]
    public void Evaluate_HonoursADeadzone()
    {
        var curve = new ResponseCurve(new[]
        {
            new CurvePoint(0.0, 0.0),
            new CurvePoint(0.3, 0.0),
            new CurvePoint(1.0, 1.0),
        });

        Assert.Equal(0.0, curve.Evaluate(0.1), 6);
        Assert.Equal(0.0, curve.Evaluate(0.3), 6);
        Assert.True(curve.Evaluate(0.4) > 0.0);
    }

    [Theory]
    [InlineData(-5.0)]
    [InlineData(-0.001)]
    [InlineData(1.001)]
    [InlineData(99.0)]
    public void Evaluate_ClampsInputOutsideZeroToOne(double input)
    {
        var result = ResponseCurve.Linear().Evaluate(input);
        Assert.InRange(result, 0.0, 1.0);
    }

    [Fact]
    public void Normalize_SortsPointsByX()
    {
        var curve = new ResponseCurve(new[]
        {
            new CurvePoint(1.0, 1.0),
            new CurvePoint(0.0, 0.0),
            new CurvePoint(0.5, 0.3),
        });

        Assert.Equal(new[] { 0.0, 0.5, 1.0 }, curve.Points.Select(p => p.X));
    }

    [Fact]
    public void Normalize_AddsMissingEndPoints()
    {
        var curve = new ResponseCurve(new[] { new CurvePoint(0.4, 0.4) });

        Assert.Equal(0.0, curve.Points[0].X, 6);
        Assert.Equal(1.0, curve.Points[^1].X, 6);
    }

    [Fact]
    public void Normalize_ClampsOutOfRangePoints()
    {
        var curve = new ResponseCurve(new[]
        {
            new CurvePoint(-1.0, -2.0),
            new CurvePoint(5.0, 9.0),
        });

        Assert.All(curve.Points, p =>
        {
            Assert.InRange(p.X, 0.0, 1.0);
            Assert.InRange(p.Y, 0.0, 1.0);
        });
    }

    [Fact]
    public void EmptyCurve_BehavesLikeLinearRatherThanThrowing()
    {
        var curve = new ResponseCurve();
        Assert.Equal(0.5, curve.Evaluate(0.5), 6);
    }

    [Fact]
    public void Clone_DoesNotShareItsPointsWithTheOriginal()
    {
        var original = ResponseCurve.Linear();
        var copy = original.Clone();

        copy.Points[0].Y = 0.9;

        Assert.Equal(0.0, original.Points[0].Y, 6);
    }
}
