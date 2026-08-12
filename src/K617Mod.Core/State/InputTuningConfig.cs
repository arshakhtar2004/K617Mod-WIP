namespace K617Mod.Core.State;

/// <summary>
/// The one piece of tuning that is a property of the hardware rather
/// than a matter of taste: the raw depth range the keyboard reports.
///
/// The response curve exponents and the digital press threshold used to
/// live here as constants too. They now come from the selected profile,
/// via <see cref="ProfileTuning"/> - those are preferences, and baking a
/// preference into a constant meant the profile editor had nothing real
/// to write to. What is left here is calibration, which is the same for
/// every profile on a given keyboard.
///
/// NOTE ON RawDepthMin/RawDepthMax: these intentionally duplicate the
/// same numbers as Hid.HidProtocolConfig rather than referencing that
/// class directly. That's a deliberate trade of a couple of duplicated
/// constants in exchange for this module having zero dependency on the
/// Hid namespace. If the keyboard's real depth range is ever
/// recalibrated, both places need the update - worth a quick search for
/// "340" across the project if that ever happens.
/// </summary>
public static class InputTuningConfig
{
    public const int RawDepthMin = 0;
    public const int RawDepthMax = 340;
}
