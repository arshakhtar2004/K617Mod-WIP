using K617Mod.Core.State;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace K617Mod.Core.Output;

/// <summary>
/// Drives a real virtual Xbox 360 controller via ViGEmBus. Windows and
/// games see this as a real, physically plugged in Xbox 360 controller.
/// Requires the ViGEmBus driver installed on the machine - the
/// constructor will throw if it isn't. This is the only class in the
/// Output namespace that touches ViGEm directly.
/// </summary>
public sealed class VigemVirtualPad : IVirtualPad
{
    private readonly ViGEmClient _client;
    private readonly IXbox360Controller _controller;

    public VigemVirtualPad()
    {
        _client = new ViGEmClient();
        _controller = _client.CreateXbox360Controller();
        _controller.Connect();

        // NOTE (unverified from this environment): assuming
        // IXbox360Controller.AutoSubmitReport defaults to true in the
        // pinned package version, meaning each SetAxisValue/SetSliderValue/
        // SetButtonState call below takes effect immediately without a
        // manual SubmitReport() call. If joy.cpl or the harness shows no
        // movement despite no errors, check that property in IntelliSense -
        // either set it true explicitly here, or add an explicit
        // _controller.SubmitReport() call at the end of Apply().
    }

    public void Apply(ControllerStateSnapshot snapshot)
    {
        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, ToAxisValue(snapshot.Steering));
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0); // unused - FH6 steering is X-axis only

        _controller.SetSliderValue(Xbox360Slider.RightTrigger, ToTriggerValue(snapshot.Accelerate));
        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, ToTriggerValue(snapshot.Brake));

        foreach (var (action, pressed) in snapshot.DigitalStates)
        {
            if (ActionButtonMap.TryGetButton(action, out var button))
            {
                _controller.SetButtonState(button, pressed);
            }
        }
    }

    public void Reset()
    {
        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);

        foreach (var button in ActionButtonMap.AllButtons)
        {
            _controller.SetButtonState(button, false);
        }
    }

    /// <summary>-1.0..1.0 normalized -> the short range a real Xbox thumbstick axis uses.</summary>
    private static short ToAxisValue(double normalized) =>
        (short)Math.Clamp(normalized * short.MaxValue, short.MinValue, short.MaxValue);

    /// <summary>0.0..1.0 normalized -> the byte range a real Xbox trigger uses.</summary>
    private static byte ToTriggerValue(double normalized) =>
        (byte)Math.Clamp(normalized * byte.MaxValue, 0, byte.MaxValue);

    public void Dispose()
    {
        _controller.Disconnect();
        _client.Dispose();
    }
}
