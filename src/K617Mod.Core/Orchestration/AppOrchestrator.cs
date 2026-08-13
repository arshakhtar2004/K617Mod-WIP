using K617Mod.Core.Hid;
using K617Mod.Core.Mapping;
using K617Mod.Core.Output;
using K617Mod.Core.State;
using K617Mod.Core.Suppression;

namespace K617Mod.Core.Orchestration;

/// <summary>
/// Wires a HID source, a key map, a virtual pad, and a key suppressor
/// into one running pipeline: raw HID reports -> key name lookup ->
/// InputState -> a fixed-rate tick loop applying snapshots to the
/// virtual pad. Direct architectural equivalent of the Python build's
/// main.py, split so the actual orchestration logic lives here in Core
/// (testable with fakes) rather than inside whatever project happens to
/// host the entry point - meaning Part 7's WPF window can call this
/// exact same class later without any of this logic being rewritten.
///
/// This class is the one place in the whole project allowed to know
/// about every other module - by design, everything it depends on comes
/// in as an interface via the constructor (composition root pattern),
/// so its own logic (threading, event wiring, fail-open suppression
/// handling) can be tested with fakes standing in for hardware/drivers.
/// </summary>
public sealed class AppOrchestrator : IDisposable
{
    private readonly IHidKeySource _hidSource;
    private readonly IInputState _inputState;
    private readonly IVirtualPad _virtualPad;
    private readonly IKeySuppressor _keySuppressor;
    private readonly bool _enableSuppression;
    private readonly int _tickIntervalMs;

    /// <summary>
    /// Volatile rather than readonly because <see cref="ApplyKeyMap"/>
    /// replaces it while the HID thread is reading it in
    /// OnReportReceived. A reference assignment is already atomic; the
    /// volatile is what guarantees the HID thread sees the new reference
    /// promptly rather than a cached one.
    /// </summary>
    private volatile IKeyMap _keyMap;

    private volatile bool _running;
    private Thread? _tickThread;

    /// <summary>Exposed so a future UI can read live state for telemetry display.</summary>
    public IInputState InputState => _inputState;

    public bool IsConnected => _hidSource.IsConnected;
    public bool SuppressionActive { get; private set; }
    public string? SuppressionError { get; private set; }

    /// <param name="tuning">
    /// Where the pipeline reads curves and the digital threshold from.
    /// Pass a <see cref="TuningSource"/> built from the selected profile
    /// to have this run that profile's tuning - and to be able to swap
    /// it while running. Omit it and the built-in defaults are used.
    /// </param>
    public AppOrchestrator(
        IHidKeySource hidSource,
        IKeyMap keyMap,
        IVirtualPad virtualPad,
        IKeySuppressor keySuppressor,
        ITuningSource? tuning = null,
        bool enableSuppression = true,
        int tickRateHz = 64)
    {
        _hidSource = hidSource;
        _keyMap = keyMap;
        _inputState = new K617Mod.Core.State.InputState(keyMap, tuning);
        _virtualPad = virtualPad;
        _keySuppressor = keySuppressor;
        _enableSuppression = enableSuppression;
        _tickIntervalMs = 1000 / tickRateHz;
    }

    /// <summary>
    /// Swap in a different profile's key map while the pipeline runs.
    ///
    /// Two things have to change together: the position -> key lookup
    /// this class does on the HID thread, and the key -> action bindings
    /// InputState holds. They are updated in that order deliberately. A
    /// report arriving in between resolves to a key name from the new map
    /// that the old bindings don't recognise, and is ignored - one
    /// dropped reading at 64Hz. The reverse order could route a reading
    /// from the *old* physical position onto a *new* action, which would
    /// briefly move the wrong control.
    ///
    /// Curves are swapped separately, through the ITuningSource this was
    /// constructed with. Kept apart because they are genuinely
    /// independent: changing a curve does not need the HID thread told
    /// anything at all.
    /// </summary>
    public void ApplyKeyMap(IKeyMap keyMap)
    {
        ArgumentNullException.ThrowIfNull(keyMap);

        _keyMap = keyMap;
        _inputState.ApplyBindings(KeyBindingSet.FromKeyMap(keyMap));
    }

    public void Start()
    {
        // Suppression first, same order as the Python build - and it
        // fails OPEN: a suppression failure is surfaced via
        // SuppressionActive/SuppressionError for the host to display,
        // but doesn't stop the rest of the pipeline from starting. A
        // person should still be able to use the analog input even if
        // suppression couldn't attach (K617 will also type normally
        // alongside it in that case, same trade-off the Python build made).
        if (_enableSuppression)
        {
            try
            {
                _keySuppressor.Start();
                SuppressionActive = true;
                SuppressionError = null;
            }
            catch (Exception ex)
            {
                SuppressionActive = false;
                SuppressionError = ex.Message;
            }
        }

        _hidSource.ReportReceived += OnReportReceived;

        try
        {
            _hidSource.Start();
        }
        catch
        {
            // Startup failed partway through - clean up what did start
            // rather than leaving suppression dangling active with no
            // way for the caller to know it needs cleanup too.
            _hidSource.ReportReceived -= OnReportReceived;
            if (SuppressionActive)
            {
                _keySuppressor.Stop();
                SuppressionActive = false;
            }
            throw;
        }

        _running = true;
        _tickThread = new Thread(TickLoop) { IsBackground = true, Name = "K617TickLoop" };
        _tickThread.Start();
    }

    public void Stop()
    {
        _running = false;
        _tickThread?.Join(TimeSpan.FromSeconds(2));
        _tickThread = null;

        _hidSource.ReportReceived -= OnReportReceived;
        _hidSource.Stop();

        if (SuppressionActive)
        {
            _keySuppressor.Stop();
            SuppressionActive = false;
        }

        _virtualPad.Reset();
    }

    /// <summary>
    /// Bridges Part 1's raw (row, col) reports to Part 3's named
    /// Update() calls - this translation deliberately doesn't live in
    /// either of those modules, since neither should need to know about
    /// the other.
    /// </summary>
    private void OnReportReceived(object? sender, RawKeyReport report)
    {
        var position = new KeyPosition(report.Row, report.Col);
        var keyName = _keyMap.FindKeyByPosition(position);
        if (keyName is null) return; // not a key currently bound to anything - silently ignored, same as the Python build

        _inputState.Update(keyName, report.Depth);
    }

    private void TickLoop()
    {
        while (_running)
        {
            var snapshot = _inputState.Snapshot();
            _virtualPad.Apply(snapshot);
            Thread.Sleep(_tickIntervalMs);
        }
    }

    public void Dispose()
    {
        Stop();
        _hidSource.Dispose();
        _virtualPad.Dispose();
        _keySuppressor.Dispose();
    }
}
