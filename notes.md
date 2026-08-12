# K617Mod

## Where the code is

`projects/professional/k617mod/src/` — moved here from the workspace root
on 8 Aug so the code and its notes live together. All 271 files verified
present after the move, and every `ProjectReference` in the `.csproj`
files is relative and internal to the tree, so nothing broke.

`K617Mod-backup.zip` sits alongside it.

## GitHub

Two repos under arshakhtar2004, both public:
- `K617Mod-MVP` — renamed from `Red-Dragon-K617-HE-controller` (12 Aug).
  Was empty at rename time — turns out the real MVP was a separate,
  earlier Python build sitting at `projects/Keyboard MVP/` (Redragon K617
  HE → Xbox 360 controller via Interception + ViGEmBus, predates the
  .NET rewrite). Pushed that there same day, excluding `__pycache__`.
- `K617Mod-WIP` — created 12 Aug, current `src/` and `notes.md` (the .NET
  rewrite, this folder) pushed here, excluding `bin/`, `obj/`, and the
  backup zip via `.gitignore`. This is the live one.

  Branches: `main` and `ui`, currently identical (`ui` was branched off
  main once the config app had its first two working pages, 12 Aug).
  Config app work continues on `ui`. A separate branch is planned for
  making profile settings importable rather than hardcoded.

  Note on pushing: the sandbox has no direct DNS or raw SSH, so pushes
  route through a proxy via
  `GIT_SSH_COMMAND="ssh -o ProxyCommand='socat - PROXY:localhost:%h:%p,proxyport=3128'"`.
  Also clone with `-c core.fileMode=false`, otherwise copying off the
  Windows folder flips permission bits on every file and every commit
  shows ~75 spurious changes.

## What it is

Turns Arsh's K617 hall-effect keyboard into a game controller with real
analog input. Hall-effect switches report how far a key is pressed, not
just whether it's pressed — so WASD can drive an analog stick the way a
thumbstick does, instead of being stuck at full-tilt digital movement.

Needs external drivers/applications to work (notably ViGEm for the virtual
gamepad).

**The goal, per Arsh (9 Aug 2026):** an interactable application to
configure how the mod works — reassigning which keys map to which
controller buttons, and creating a custom response curve (what input
depth produces what output value). Confirmed this doesn't exist yet: the
app is currently a console program with no UI, and `Program.cs` carries
its own comment noting the curve-shaping fields on a profile
(`SteeringCurveExponent`, `ThrottleBrakeCurveExponent`,
`DigitalPressThreshold`) aren't wired into `InputState` — it still reads
fixed constants from `InputTuningConfig` instead. The comment calls this
out as a gap waiting on "whenever a curve-editing UI exists." So this is
new build, not polish of something already working.

## Stack

.NET 8 / C#. No solution file as of the current README — projects are run
directly via `dotnet run` or added manually in Visual Studio.

## Architecture

Deliberately modular, each layer depending on an interface rather than a
concrete class, so pieces can be swapped or tested alone.

| Module | Responsibility |
|--------|----------------|
| `Hid/` | Opens the analog HID interface, yields raw `(row, col, depth, mode)` reports. Knows nothing about key names or controllers. |
| `Mapping/` | Pure lookups: key name → HID position, key name → controller action + input type. No I/O beyond `KeyMapLoader`. |
| `State/` | Holds current input state, applies tuning, produces `ControllerStateSnapshot`. |
| `Output/` | `IVirtualPad` → `VigemVirtualPad`. Pushes state to the virtual controller. |
| `Suppression/` | Stops raw keystrokes reaching Windows while in controller mode. Includes `HardwareIdMatcher`. |
| `Persistence/` | Profiles and app settings as JSON. |
| `Orchestration/` | `AppOrchestrator` wires the pipeline together. |

Test harnesses (`DevHarness`, `OutputHarness`, `SuppressionHarness`) each
prove one layer works standalone.

## Design principle Arsh follows here

Downstream code references the interface (`IHidKeySource`, `IKeyMap`,
`IVirtualPad`), never the implementation. Config holds protocol facts only
and stays ignorant of higher-level concepts like "key name". Data lives in
JSON, not code.

## Docs

`README-Part1.md` through `README-Part8.md`. Part 7 is the final part and
hasn't been written yet — not lost, just not done. Each covers one module,
written as build-and-verify guides.

## Current priority (updated 9 Aug 2026)

One of two current priorities (equal weight with upskilling, see
`TASKS.md`): **finish this in polished application form**, because it is
going on his resume and needs to be presentable to a recruiter.

That reframes the work. "Done" is no longer "it functions" — it's "someone
who has never seen it can understand what it does and judge it in about
ninety seconds." Concretely that tends to mean:

- A single coherent README at the front, not eight numbered parts. The
  part-files are good build documentation, but a recruiter will read the
  first screen and stop.
- A demo — a GIF or short video of a keyboard driving an analog stick is
  worth more than any paragraph, because the whole idea is visual and
  immediately legible.
- Clear articulation of *why it's non-obvious*. Hall-effect depth sensing
  is the interesting part; without that framing it reads as "remapped some
  keys."
- Working build instructions someone can actually follow.
- Visible tests. The existing harnesses are a genuine strength and are
  currently invisible from outside.
- A solution file, since there isn't one — a project that doesn't open
  cleanly loses the reader immediately.

None of this is decided. It's the shape of the problem, for Arsh to
direct.

The one confirmed functional requirement so far: a working config app for
key-to-button reassignment and response-curve editing. Everything above
(single README, demo, solution file, visible tests) still applies, but
now on top of an app that has to actually be built, not just repackaged.

## Config app — scope (decided 9 Aug 2026)

Three decisions made, before any UI stack is picked or code written:

**1. Key remap.** User picks an action from the existing action list (the
same names `ActionButtonMap` and `keymapping.default.json` already use —
`A_HANDBRAKE`, `STEER_LEFT`, etc.), then picks which physical key drives
it. This edits `KeyMapDocument.ControllerMap` for the active profile.
Explicitly *not* in scope: changing which Xbox button an action fires
(`ActionButtonMap` stays fixed in code) — only which key triggers an
already-defined action.

**2. Response curve editor.** Visual — a draggable curve graph, not
number fields. Edits the per-profile `SteeringCurveExponent` /
`ThrottleBrakeCurveExponent` (and probably surfaces
`DigitalPressThreshold` too, though that one's a threshold, not a curve).

**3. Wiring fix comes first.** Before the editor is built,
`InputState.Snapshot()` needs to read `SteeringCurveExponent` /
`ThrottleBrakeCurveExponent` / `DigitalPressThreshold` from the loaded
profile instead of the hardcoded `InputTuningConfig` constants
([InputState.cs:76-81](src/K617Mod.Core/State/InputState.cs)). Decided
now rather than after the UI, so the editor changes something real from
the moment it exists instead of writing to a value nothing reads.

## Config app — decisions (12 Aug 2026)

Supersedes the "not yet decided" list above. All settled with Arsh.

**UI stack: WPF.** Matches what the code already assumed — both
`AppOrchestrator` and `IProfileStore` carry comments referring to "Part
7's WPF window." New `K617Mod.Ui` project (net8.0-windows, `UseWPF`)
rather than converting `K617Mod.App`, so the console harness survives as
a test path. A `.sln` gets added at the same time — it was already on the
presentable-state list.

**Curve model: multi-point, not exponent.** Each curve is a list of
points, interpolated between, not `output = input^n`. This allows
deadzones and S-curves, which a single exponent can't express. Cost:
`ProfileDocument` schema changes, `InputState` interpolates instead of
calling `Math.Pow`, and it's meaningfully more work than the exponent
version.

**Three curves, independent:** accelerate, brake, and steering (one
curve, both left and right follow it). Replaces the current
`SteeringCurveExponent` + shared `ThrottleBrakeCurveExponent` pair.

**Mod has a master ON/OFF.** OFF stops the orchestrator — suppression
released, keyboard types normally. This makes the old `Typing` profile
redundant, since OFF now does that job.

**Five profiles when ON:** 1 default that can't be edited, plus 4
editable copies of it.

**Save applies immediately.** "Save changes to current profile" writes to
the profile's config file *and* the running pipeline picks it up without
a restart. Implies the tuning object must be safe to read from the tick
thread while the UI thread replaces it.

**Known consequence — existing saved profiles.** The `FH6` and `Typing`
profiles already in `%AppData%/K617Mod/profiles/` predate the new schema.
They won't carry curve data, so they'll load with default curves rather
than crash (System.Text.Json leaves missing properties at their
defaults). Key mappings survive. Cleanest is to delete both files and let
the bootstrapper regenerate.
