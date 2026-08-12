namespace K617Mod.Core.State;

/// <summary>
/// Tuning constants for turning a raw depth reading into a controller
/// value: the normalization range and the response curve exponents.
/// Direct port of the response-curve section of the Python build's
/// config.py.
///
/// NOTE ON RawDepthMin/RawDepthMax: these intentionally duplicate the
/// same numbers as Hid.HidProtocolConfig rather than referencing that
/// class directly. That's a deliberate trade of a few duplicated
/// constants in exchange for this module having zero dependency on the
/// Hid namespace - keeps Part 3 testable and reasoned-about in complete
/// isolation from Part 1, matching the independence goal. If the
/// keyboard's real depth range is ever recalibrated, both places need
/// the update - worth a quick search for "340" across the project if
/// that ever happens.
/// </summary>
public static class InputTuningConfig
{
    public const int RawDepthMin = 0;
    public const int RawDepthMax = 340;

    /// <summary>
    /// Applied to throttle/brake after linear normalization:
    /// output = normalized ** exponent. 2.0 means 50% key depth becomes
    /// 25% throttle/brake, softening shallow presses while 0% and 100%
    /// stay fixed points.
    /// </summary>
    public const double ThrottleBrakeCurveExponent = 2.0;

    /// <summary>
    /// Left linear (1.0) on purpose - squaring steering input makes
    /// small corrections mushy and full lock twitchy. Raise later if
    /// that's actually the feel wanted.
    /// </summary>
    public const double SteeringCurveExponent = 1.0;

    /// <summary>
    /// Fraction of full depth at which a DIGITAL-type key counts as
    /// "pressed." One shared threshold for every digital key for now.
    /// </summary>
    public const double DigitalPressThreshold = 0.3;
}
