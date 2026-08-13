# K617 HE Racing Mod

Turns a Redragon K617 HE (hall-effect) keyboard into an analog Xbox 360
controller. Full key travel depth becomes analog stick/trigger input,
not just on/off — steering, throttle and brake read as continuous
values, the way a real controller does, driven entirely by how far a
key is pressed.

The K617 has no documented protocol for this. Everything the mod knows
about its analog HID reports and its "wake" requirement came from
capturing and reverse-engineering the keyboard's own traffic — see
`notes.md` for that process.

## What it does

- **Analog input from key depth.** Reads the K617's raw HID reports and
  turns travel depth into a normalized 0–1 value per key.
- **Full Xbox 360 control surface.** All 25 controls — both sticks, both
  triggers, D-pad, face buttons, bumpers, stick clicks, Guide — can be
  bound to a physical key. Steering, throttle and brake are analog;
  everything else is digital (press/release).
- **Per-axis response curves.** Six axes (both sticks' X/Y, both
  triggers), each with its own multi-point curve rather than a fixed
  exponent — so a curve can express a deadzone or an S-curve, not just
  "softer" or "sharper". Draggable editor with linear/soft/sharp/deadzone
  presets.
- **Five profiles, switching live.** One read-only `Default` baseline
  plus four editable slots, started as copies of it. Selecting a
  different profile — or saving an edit to the one already selected —
  takes effect on a running mod immediately, with no restart and no
  visible interruption to whatever the controller is plugged into.
- **Tray-first.** Launching the exe shows no window: a tray icon and the
  mod starting (or not) in whatever mode it was last explicitly left in.
  The config window opens from the tray when it's wanted; closing it
  hides it rather than quitting.
- **Automatic wake.** The K617's analog interface needs a specific
  packet sent once after it's plugged in, or it will accept keystrokes
  but report no depth data. The mod sends this itself on connect — no
  more opening iLumiPC's Travel Test page once per boot.
- **Un-woken-device guard.** If the analog interface somehow stays
  silent while suppression is swallowing keystrokes — the signature of
  the wake step having failed — the mod stops itself and hands typing
  back, rather than leaving a keyboard that does nothing at all.
- **Suppression, fails open.** The K617's own keystrokes are blocked
  while the mod runs (via the Interception driver), so it doesn't also
  type into whatever it's controlling. If suppression can't attach, the
  mod still runs rather than refusing to start — the keyboard just also
  types, which is visible and better than silently doing nothing.

## Status

Confirmed running on real hardware: analog wake, key suppression,
tray-first startup, in-window ON/OFF, and profile selection all verified
working end to end. `dotnet test` passes 134/134 on Windows, including
the profile-swap and startup-resolution logic added most recently.

The single-file publish (`src\publish.cmd` → `src\dist\K617Mod.exe`)
also builds and launches successfully on Windows.

**Not yet confirmed:** switching profiles *while the mod is actively
running* has been implemented and is covered by unit and integration
tests (including a concurrent-swap stress test), but hasn't yet had a
hands-on check with the keyboard attached and a game running. That's
the next thing to verify.

## Architecture

Three-project split, each independently testable:

- **`K617Mod.Core`** — everything hardware/logic-facing: HID reading,
  key mapping, suppression, virtual pad output, response curves, profile
  persistence, and `AppOrchestrator`, which wires all of it into one
  running pipeline. No UI code anywhere in here.
- **`K617Mod.Ui`** — the WPF/WinForms hybrid config app and tray icon.
  `ModController` is the composition root that owns the running
  pipeline; `ProfileSession` owns the profile editor's state. `App.xaml.cs`
  is the only place the two are connected to each other.
- **`K617Mod.App`** — a plain console host, useful for testing the core
  pipeline without any UI in the way.

Everything hardware-facing sits behind an interface (`IHidKeySource`,
`IVirtualPad`, `IKeySuppressor`, `IKeyMap`…), so `K617Mod.Core.Tests`
exercises the real orchestration, threading and profile-swap logic
against fakes — no keyboard, ViGEmBus or Interception driver needed to
run the test suite.

The incremental build log for each part of this (`src/README-Part1.md`
through `README-Part8.md`) has the full reasoning behind each piece, in
the order it was built. `notes.md` is the day-to-day decision log,
including the hardware reverse-engineering work.

## Requirements

- Windows 10/11, x64.
- [ViGEmBus](https://github.com/nefarius/ViGEmBus) — the virtual
  Xbox 360 controller driver.
- [Interception](https://github.com/oblitum/Interception) — the keyboard
  suppression driver. Needs a reboot after install.
- Administrator rights to run (the suppression driver requires it — see
  `src/K617Mod.Ui/app.manifest`).
- .NET 8 SDK, only if building from source. The published exe is
  self-contained and needs nothing installed to run.

## Build & run

**Run the tests (no hardware needed):**
```
dotnet test src\K617Mod.Core.Tests
```

**Build a standalone exe:**
```
.\src\publish.cmd
```
Produces `src\dist\K617Mod.exe` — one file, self-contained, no .NET
install required on the machine that runs it. Full procedure and
troubleshooting in `publish-runbook.md`.

**Run from source instead (console host, useful for debugging the
pipeline directly):**
```
cd src\K617Mod.App
dotnet run
```

## Open questions before this goes on a resume

- **Does the K617 HE firmware already have a native Xbox 360 gamepad
  mode?** iLumiPC's own UI exposes a "gamepad mode" setting with
  Xbox360/Classical options. If the board already does some of this in
  hardware, the README needs to say so honestly rather than imply the
  mod invented capability the keyboard already had. The mod would still
  add curves, remapping, profiles and suppression on top either way —
  but the framing needs checking first.
- **Prior art.** `adapt-to-it/he-analog-gamepad` is reportedly the same
  idea for a different board — not yet looked into here, so nothing
  about it is claimed beyond the name. Worth reading before writing
  anything comparative.
