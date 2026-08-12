# Part 4 - Virtual Controller Output Module

## What this is

Takes a `ControllerStateSnapshot` (from Part 3) and drives a virtual
Xbox 360 controller via ViGEmBus. Windows and games see it as a real,
physically plugged-in controller.

## Structure

```
K617Mod.Core/
  Output/
    IVirtualPad.cs                - the contract; Part 8 (orchestrator) will depend on this
    VigemVirtualPad.cs              - real ViGEmBus implementation
    ActionButtonMap.cs                - digital action name -> Xbox360Button

K617Mod.Core.Tests/
  Output/
    FakeVirtualPad.cs              - test double, records what was sent, no ViGEm involved
    ActionButtonMapTests.cs          - pure logic tests, no driver needed
    ActionButtonMapAgainstDefaultMappingTests.cs - catches drift between the JSON and this map

K617Mod.OutputHarness/
  Program.cs                       - sweeps synthetic values through a REAL virtual controller
```

## One prerequisite before the harness will work: ViGEmBus

Unlike Parts 1-3, this part needs a driver installed on your machine
before the *harness* will connect (the automated tests don't need it -
more on that below).

1. Download and install **ViGEmBus** from the official Nefarius releases
   page (search "ViGEmBus releases" or check the project's GitHub - grab
   the latest signed installer for your Windows version).
2. Reboot if the installer asks for one.
3. Confirm it installed: Device Manager -> System devices -> look for
   **"Nefarius Virtual Gamepad Emulation Bus"**.

## Why it's structured this way

- **`ActionButtonMap` is the only place that knows which Xbox button a
  digital action drives.** If a button ever needs remapping (e.g.
  handbrake moved from A to a different button), this is the only file
  that changes.
- **`VigemVirtualPad` is the only class that touches ViGEm directly.**
  Everything else - including the orchestrator later - depends on
  `IVirtualPad` instead, which is what makes `FakeVirtualPad` possible:
  the wiring logic gets tested without the driver installed at all.
- **Analog actions never appear in `ActionButtonMap` on purpose** - one
  of the tests (`AnalogActionNames_AreNotInTheButtonMap`) specifically
  checks this, since accidentally routing steering through a button
  would be a real, confusing bug.

## Build & test steps

**Automated tests - no ViGEmBus needed for these:**
```
cd K617Mod.Core.Tests
dotnet test
```
These only exercise `ActionButtonMap`'s dictionary and `FakeVirtualPad` -
neither touches a real driver. Expect the previous 30 plus roughly 10
more from this part.

**Hardware harness - needs ViGEmBus installed:**
```
cd K617Mod.OutputHarness
dotnet run
```
1. Open Windows' `joy.cpl` (Run -> `joy.cpl`) *before or right after*
   starting the harness.
2. You should see an "Xbox 360 Controller" appear in the device list.
3. Open its properties - the left stick should sweep smoothly left/right
   on its own, both triggers should pulse, and the button indicators
   should toggle roughly every 2 seconds.
4. `Ctrl+C` to stop - the pad releases and disappears from `joy.cpl`
   immediately.

## Known uncertainty flagged in the code

`VigemVirtualPad` assumes `IXbox360Controller.AutoSubmitReport` defaults
to `true` in the installed package version - meaning each `Set*` call
takes effect immediately with no separate "send" step. This wasn't
verified against a live build from this environment. If the harness
connects (shows up in `joy.cpl`) but nothing visibly moves, that
property is the first thing to check - flagged directly in
`VigemVirtualPad.cs`'s constructor comment with the exact fix.

## Next part

Part 5 (Key Suppression, via the Interception driver) is next - fully
self-contained, and testable in isolation the same way this part's
harness works: run it, confirm the K617 stops typing while other
keyboards keep working normally.
