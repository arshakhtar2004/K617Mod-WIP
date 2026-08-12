# Part 2 - Key Mapping Module

## What this is

Pure data + lookups: physical key name -> raw HID position, and physical
key name -> controller action + input type. No hardware, no HID protocol
knowledge, no ViGEm, no UI. This is the most independent module in the
whole project - it can be built and fully tested with hand-typed numbers,
with nothing else in the solution involved at all.

## Structure

```
K617Mod.Core/
  Mapping/
    IKeyMap.cs              - the contract; Part 3 will depend on this, not KeyMap directly
    KeyMap.cs                 - pure in-memory implementation (no I/O)
    KeyPosition.cs             - (row, col) value type
    KeyBinding.cs               - (action, InputType) value type
    InputType.cs                 - Analog / Digital
    KeyMapDocument.cs             - JSON wire-format DTOs (isolated from KeyMap itself)
    KeyMapLoader.cs                - the only class that knows JSON exists
    Data/
      keymapping.default.json       - the actual current mapping, as data not code

K617Mod.Core.Tests/
  Mapping/
    KeyMapTests.cs             - tests KeyMap directly, hand-written data, zero I/O
    KeyMapLoaderTests.cs        - loads the real shipped JSON, catches data-file typos
```

## Why it's structured this way

- **KeyMap never touches JSON.** It just answers questions about two
  dictionaries handed to it in its constructor. That's what makes
  `KeyMapTests.cs` possible without any file on disk at all - and it
  means if the storage format ever changes (a database, a settings UI
  writing its own format), only `KeyMapLoader` needs to change.
- **The mapping is data (JSON), not code**, unlike the Python version's
  hard-coded dictionaries. This is a deliberate upgrade, not just a
  straight port - it's what makes an in-app "remap this key" UI
  feasible later without a recompile.
- **Two separate test files, on purpose**: `KeyMapTests` proves the
  *logic* is correct with trivial made-up data; `KeyMapLoaderTests`
  proves the *actual shipped file* is correct. A bug could hide from one
  and not the other - e.g. a correct loader parsing a JSON file with a
  typo'd row number would only get caught by the second file.

## Build & test steps

1. From the `K617Mod` folder:
   ```
   cd K617Mod.Core.Tests
   dotnet test
   ```
   (needs `dotnet restore` to pull xunit + the test SDK from NuGet first,
   same as Part 1's HidSharp dependency - normal internet access needed
   on the machine you build this on)
2. All tests should pass with zero hardware plugged in, since nothing
   here touches a device. If `KeyMapLoaderTests` fails but `KeyMapTests`
   passes, the bug is in `keymapping.default.json`, not in the code.

## Next part

Part 3 (Input State / Mapper) will take `IKeyMap` plus raw depth values
and produce the actual axis/button values the controller needs. It can
be built and tested with fake depth numbers, still without the real
keyboard, HidSharp, or ViGEm involved - same independence principle,
one level up.
