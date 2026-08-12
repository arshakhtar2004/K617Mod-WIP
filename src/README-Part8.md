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

## A known, deliberate gap - flagged, not forgotten

Part 6 made curve exponents and the digital-press threshold per-profile
values instead of fixed constants. `Program.cs` loads those values from
the active profile but **doesn't actually pass them into `InputState`
yet** - `InputState` still reads from Part 3's fixed
`InputTuningConfig` constants. Right now this causes no visible
difference (the bootstrapped FH6 profile's default values happen to
exactly match those constants), so it's safe to defer rather than solve
today. The natural point to wire this properly is whenever the
curve-editing UI arrives, since that's the first time someone could
actually enter a *different* value and expect to feel it. Flagged
directly in `Program.cs`'s comments too.

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

1. Hold a key on the K617 HE, then press Enter when prompted (interface
   detection, same as every hardware harness so far).
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
