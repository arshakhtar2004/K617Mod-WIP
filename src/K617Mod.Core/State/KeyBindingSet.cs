using K617Mod.Core.Mapping;

namespace K617Mod.Core.State;

/// <summary>One digital control and the physical key that presses it.</summary>
public readonly record struct DigitalBinding(string Action, string KeyName);

/// <summary>
/// The mapping half of a profile - which physical key drives which
/// controller action - in the shape the input pipeline actually consumes.
/// The exact counterpart of <see cref="ProfileTuning"/>, which does the
/// same job for the curves half.
///
/// This exists so a remap can be applied to a *running* pipeline. Before
/// it, InputState read an IKeyMap once in its constructor and cached the
/// answers in readonly fields, which meant changing a binding needed the
/// whole pipeline torn down and rebuilt - dropping suppression and the
/// virtual pad, so the game saw the controller disconnect and reconnect
/// every time a key was reassigned. Curves never had that problem, and
/// there was no good reason for the two halves of one profile to behave
/// differently on Apply.
///
/// Immutable once built, for the same reason ProfileTuning is: changing
/// the bindings means constructing a new set and swapping it in, never
/// mutating one a tick might already be reading.
///
/// Deliberately holds analog bindings as an action -> key dictionary
/// rather than a field per control. InputState still owns the knowledge
/// of *which* action ids are analog and what they mean; this class only
/// knows that some actions are analog and some are digital. Adding a
/// seventh analog control therefore touches InputState alone.
/// </summary>
public sealed class KeyBindingSet
{
    private readonly Dictionary<string, string> _analogKeyByAction;

    public KeyBindingSet(
        IReadOnlyCollection<string> boundKeys,
        IReadOnlyDictionary<string, string> analogKeyByAction,
        IReadOnlyList<DigitalBinding> digitalBindings)
    {
        BoundKeys = boundKeys;
        DigitalBindings = digitalBindings;
        _analogKeyByAction = new Dictionary<string, string>(analogKeyByAction, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Nothing bound to anything. What an empty profile behaves like.</summary>
    public static KeyBindingSet Empty { get; } = new(
        Array.Empty<string>(),
        new Dictionary<string, string>(),
        Array.Empty<DigitalBinding>());

    /// <summary>Every physical key name bound to some controller action.</summary>
    public IReadOnlyCollection<string> BoundKeys { get; }

    /// <summary>Every digital control and the key that presses it.</summary>
    public IReadOnlyList<DigitalBinding> DigitalBindings { get; }

    /// <summary>The key driving an analog action, or null if nothing is bound to it.</summary>
    public string? KeyForAnalogAction(string actionId) =>
        _analogKeyByAction.TryGetValue(actionId, out var keyName) ? keyName : null;

    /// <summary>
    /// Flattens an IKeyMap into the lookups the pipeline needs. Done once
    /// per profile change rather than once per tick, which is the whole
    /// point of this type existing rather than InputState querying the
    /// key map directly on the hot path.
    ///
    /// If two keys are bound to the same analog action - which the editor
    /// prevents, but a hand-edited JSON file could still contain - the
    /// last one enumerated wins. Same behaviour as before this class,
    /// deliberately preserved rather than made an error.
    /// </summary>
    public static KeyBindingSet FromKeyMap(IKeyMap keyMap)
    {
        var analog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var digital = new List<DigitalBinding>();
        var bound = new List<string>();

        foreach (var keyName in keyMap.BoundKeys)
        {
            bound.Add(keyName);

            var binding = keyMap.GetControllerAction(keyName);
            if (binding is null) continue;

            if (binding.Value.Kind == InputType.Digital)
            {
                digital.Add(new DigitalBinding(binding.Value.Action, keyName));
            }
            else
            {
                analog[binding.Value.Action] = keyName;
            }
        }

        return new KeyBindingSet(bound, analog, digital);
    }
}
