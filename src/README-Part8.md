# Part 8 - Orchestrator + Real Runnable App

## What this is

The piece that turns six separately-proven modules into one actual
running mod: raw HID reports -> key name lookup -> InputState -> a
fixed-rate tick loop -> the virtual controller, with suppression
attached alongside. Direct architectural equivalent of the Python
build's `main.py`.

**This is the first part where you get a genuinely working, testable-in-
Forza-Horizon-6 application** - not just an isolated, individually-
confirmed module.

## Structure

```
K617Mod.Core/
  Orchestration/
    AppOrchestrator.cs         - the actual wiring logic (lives in Core, not the host)

  Persistence/
    ProfileBootstrapper.cs     - ensures Typing + FH6 profiles exist, resolves startup profile

K617Mod.Core.Tests/
  Orchestration/
    FakeHidKeySource.cs        - test double, also an early "Simulated Input Source"
    FakeKeySuppressor.cs         - test double
    AppOrchestratorTests.cs        - wiring/threading tests, zero hardware

  Persistence/
    ProfileBootstrapperTests.cs  - hermetic, temp directory

K617Mod.App/
  Program.cs                   - the REAL console entry point - run this one in Forza
```

## Why the Orchestrator lives in Core, not in K617Mod.App

`AppOrchestrator` is a plain class in `K617Mod.Core.Orchestration` - the
console project (`K617Mod.App`) is just a thin `Program.cs` that
constructs real implementations and hands them to it. This matters for
what happens next: when Part 7's WPF window arrives, it can call this
exact same `AppOrchestrator` class the exact same way - only the outer
shell (console vs. window) changes. None of today's wiring/threading
logic gets thrown away or rewritten.

## Profiles reaching the running pipeline

Part 6 made curves and the digital-press threshold per-profile values
instead of fixed constants. Two follow-ups closed the loop.

**Tuning.** `InputState` reads curves and the threshold through an
`ITuningSource` rather than from `InputTuningConfig`, so the numbers a
profile holds are the numbers the pipeline uses. `InputTuningConfig` is
now only the raw depth range, which is hardware calibration rather than
preference.

**Bindings.** `InputState` used to read an `IKeyMap` once in its
constructor and cache the answers in readonly fields, which meant a
remap could only take effect by rebuilding the whole pipeline -
dropping suppression and the virtual pad, so the game saw the controller
disconnect and reconnect every time a key was reassigned. `KeyBindingSet`
is the mapping half of a profile in the shape the pipeline consumes, the
exact counterpart of `ProfileTuning`, and `InputState.ApplyBindings` /
`AppOrchestrator.ApplyKeyMap` swap it while running.

The two halves are swapped by different mechanisms on purpose. Tuning is
read once per tick and nothing else touches it, so a volatile reference
assignment is enough and the hot path stays lock-free. Bindings are read
together with the mutable depth values they index into, so both change
under the lock `Update()` and `Snapshot()` already share - a lock taken
64 times a second regardless, against a swap that happens when a person
clicks something.

Net effect: selecting a profile, or saving an edit to the one already
selected, takes effect immediately on a running mod with no interruption
the game can see. `ModController.ApplyProfile` is the single entry point
for that, and `App.xaml.cs` is the only place the profile editor and the
running mod know about each other.

## Build & test steps

**Automated first, no hardware:**
```
cd K617Mod.Core.Tests
dotnet test
```
`AppOrchestratorTests` exercises real threading and event wiring using
fakes for the HID source and suppressor (reusing Part 4's
`FakeVirtualPad` too) - genuinely tests the orchestration logic, not
just a placeholder. Expect the previous 64 plus roughly 15-16 more.

**The real thing - needs ViGEmBus, Interception, and Administrator:**
```
cd K617Mod.App
dotnet run
```
(Run the terminal as Administrator - suppression needs it, same as
Part 5.)

1. It connects straight away - no key needs holding. Interface selection
   now reads the report descriptor's usage page instead of probing for
   live data (the older harnesses in Parts 1-5 still use the probe).
2. Confirm it says **Connected: True** and **Suppression: ACTIVE**.
3. Open Forza Horizon 6, set the controller scheme, and actually drive.
4. Press Enter in the console to stop - K617 typing should return to
   normal and the virtual controller should disappear from `joy.cpl`.

If suppression shows as OFF with an error instead of ACTIVE, that
mirrors Part 5's known prerequisites (driver installed + reboot, app run
as Administrator) - not a new bug, the same checklist as before applies.

## Next part

With a real, working, driveable mod now confirmed, Part 7 (the WPF UI)
is what's left from the original plan - built around this proven
orchestrator rather than pointed at an unverified one.
