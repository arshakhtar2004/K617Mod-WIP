# Part 3 - Input State / Mapper Module

## What this is

Takes raw depth readings for individual keys and turns them into the
actual values the controller needs: one steering axis, two trigger
values, and a pressed/released state for every digital action. No
hardware, no ViGEm, no UI - pure math over numbers you can make up by
hand.

## Structure

```
K617Mod.Core/
  State/
    IInputState.cs                - the contract; Part 4 (Output) and Part 7 (UI) depend on this
    InputState.cs                   - the implementation (thread-safe)
    ControllerStateSnapshot.cs        - one tick's worth of output data
    InputTuningConfig.cs                - curve exponents, digital threshold, depth range

K617Mod.Core.Tests/
  State/
    InputStateTests.cs               - hand-built tiny key map, fake depth values
    InputStateWithDefaultMappingTests.cs - real keymapping.default.json wired in
```

## Why it's structured this way

- **Looks up keys by action name, not hardcoded letters.** The original
  Python `mapper.py` referenced `"J"`/`"L"`/`"I"`/`"K"` directly in its
  logic. This version asks the `IKeyMap` which physical key is bound to
  `"STEER_LEFT"`, etc. Remap steering to different keys in the JSON file
  later, and this module needs zero changes - proven by the
  `RemappingActionToDifferentKey...` test, which binds `STEER_LEFT` to a
  totally different key and confirms `InputState` still finds it.
- **Two test files again, same reasoning as Part 2.** `InputStateTests`
  proves the math (curves, clamping, threshold) is correct with tiny
  made-up data. `InputStateWithDefaultMappingTests` proves the *real*
  JSON's action-name strings actually match what this module is looking
  for - a mismatch there wouldn't throw an error, it'd just silently do
  nothing, which is exactly the kind of bug worth a dedicated test for.
- **`InputTuningConfig` duplicates two numbers from Part 1's
  `HidProtocolConfig`** (the depth range, 0-340) rather than referencing
  it. Explained in that file's comments - it's a deliberate trade-off to
  keep this module at zero dependency on the Hid namespace.

## Build & test steps

Same as Part 2 - no hardware needed:
```
cd K617Mod.Core.Tests
dotnet test
```
Expect all tests from Parts 2 and 3 to run together now (they're in the
same test project) - something like `Passed! - Failed: 0, Passed: 29...`
(13 from Part 2 + 16 new from Part 3). If the total looks different from
that, paste the exact output back rather than guessing what changed.

## Next part

Part 4 (Virtual Controller Output) takes a `ControllerStateSnapshot` and
drives ViGEmBus. It can be built and tested against a **mock** pad
first (asserting the right values were sent) before ever touching a real
virtual controller or ViGEmBus install - same independence principle,
one level further up the chain.
