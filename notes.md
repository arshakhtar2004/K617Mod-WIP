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

## Startup — decided and built 12 Aug 2026

Branch `tray-startup`, off `profile-settings`. Goal was click-the-exe to
running-controller with nothing else involved.

**Launch is tray-only and silent.** No window. `App.xaml` lost its
`StartupUri` and gained `ShutdownMode="OnExplicitShutdown"` — without the
second, the process would end the first time the config window is closed,
because the default mode quits when the last window goes.

**The mod auto-starts.** Clicking the exe is the whole interaction; the
tray colour reports whether it worked. Green = running and suppressing,
orange = running but keys ALSO type, red = stopped or failed. Same scheme
as the Python build, deliberately.

**The X button hides the window, it doesn't quit.** Quit is the tray menu
only, which is the one place it's unambiguous. Reopening returns the same
window with pending edits intact.

**Single instance, enforced by a named mutex.** Two copies would fight
over the HID interface and each make their own virtual pad, which surfaces
as an opaque "device in use" error instead of anything explicable.

**Tray via WinForms `NotifyIcon`, not Hardcodet.NotifyIcon.Wpf.** WPF has
no tray icon of its own. WinForms' is built in, so this costs a build flag
instead of a third-party package — which matters for a repo someone else
is meant to clone and build. The cost is real though: `UseWindowsForms`
adds `System.Drawing` and `System.Windows.Forms` as *global* implicit
usings, and `CurveEditor.xaml.cs` already uses `Point` and `Color`
unqualified from `System.Windows.Media`. Both names exist in
`System.Drawing` too, so every one of those references would go ambiguous.
Fixed by removing both implicit usings in the csproj; WinForms is opt-in
per file via an alias in `TrayIcon.cs`.

**`ModController` is the composition root**, holding the pipeline
lifecycle, and `TrayIcon` is a pure view over it. Split on purpose so
start/stop isn't buried inside a UI widget.

## Interface detection — now instant

`K617HidSource` used to pick its interface by *probing each candidate for
live data* — up to 400 ms × 4 attempts each — which is why the console app
said "Hold a key on the K617 HE NOW, then press Enter." Unusable for a
click-to-launch app.

Selection is now by **identity**: match usage page `0xFF1B` in the
device's own report descriptor, which is what distinguishes the analog
interface from the ordinary keyboard interface the device also exposes.
Instant, no key held, no live data. Same basis the Python build used in
`hid_reader.find_device_path()`. The old data probe survives as a fallback
only, since a descriptor read can fail on some driver stacks.

Verified against `HidSharp.dll` 2.1.0 metadata that `GetReportDescriptor`,
`DeviceItems`, `Usages` and `GetAllValues` all exist. Not compiled — see
the build note below.

## Interface inventory — measured 13 Aug 2026, un-woken

First capture harness run, 3 seconds, immediately after a reboot with no
wake done. **Zero reports** — which is the un-woken device confirmed
rather than a fault.

| # | Path | Usage | In | Out | Opened |
|---|------|-------|----|-----|--------|
| 0 | `mi_01&col03` | `0001:0080` (System Control) | 2 | 0 | yes |
| 1 | `mi_01&col04` | `0001:0006` (Keyboard) | 21 | 0 | **no** |
| 2 | `mi_01&col02` | `000C:0001` (Consumer) | 3 | 0 | yes |
| 3 | `mi_00` | `0001:0006` (Keyboard) | 9 | 2 | **no** |
| 4 | `mi_02` | **`FF1B:0091`** (vendor analog) | 64 | 64 | yes |
| 5 | `mi_01&col01` | `0001:0002` (Mouse) | 7 | 0 | **no** |

Three things worth keeping:

**Interface 4 is the analog one, and it's unique.** Only `mi_02` carries
usage page `0xFF1B`, so the usage-page selection in `K617HidSource` picks
exactly one candidate. Every interface returned a readable report
descriptor, so the data-probe fallback should never fire on this machine.

**Interfaces 1, 3 and 5 can't be opened**, and that's normal — Windows
holds keyboard and mouse top-level collections exclusively. Not a
permissions problem, nothing to fix.

**The analog interface has `MaxOutputReportLength=64` and
`MaxFeatureReportLength=0`.** It does not support feature reports at all.
So the wake sequence must be an OUTPUT report, not a feature report -
which means iLumiPC is calling `sendReport`, not `sendFeatureReport`, and
`TrySendWakeReports` was reordered to try `Write` first.

## Awake capture — 13 Aug 2026, after the iLumiPC wake

Second run, same 3 seconds. **254 reports, all on the vendor interface,
all 64 bytes, all matching the documented layout, zero unrecognised.**
The protocol constants are now confirmed against live data rather than
inherited from the Python build.

**Depth range measured 0 to 340** — exactly `RawDepthMax`. Nothing above
it, so the sanity ceiling never fired.

**One key per report, confirmed.** Byte[5] is `01` on every single
report. Two keys were genuinely held at once (160 sampled moments with
two non-zero depths — `(3,2)` pinned at 340 while `(3,3)` ramped 72→287),
and the device interleaved separate reports rather than packing both into
one. So reading a single `(row, col, depth)` per report is right, and
there is no dropped-input bug hiding in simultaneous presses. Worth
having checked: accelerate-plus-steer is the normal racing case.

**Interface enumeration order is NOT stable between runs.** Index 1 was
the Keyboard collection in the first capture and the Consumer collection
in the second. Selecting by index or by "first candidate" would be a coin
toss; identity by usage page is the only thing that holds.

**Mode 3 (Summary) reports carry a much larger payload** — mode 5 is
empty past byte 9, mode 3 runs out to byte 35. Their depth field is only
ever `0000` or `0154` (=340), so they act as endpoint markers at
bottom-out and release rather than intermediate readings. Accepting them
alongside mode 5, as the parser already does, is therefore harmless and
gives a clean zero on release.

The rest of the mode-3 payload is *inferred, not confirmed*: byte 16 is
1 on bottom-out and 0 on release; bytes 12-15 and 19-22 hold 16-bit values
clustered around 0x0840 and 0x04e0, which look like per-key rest and
pressed ADC readings; bytes 10-11 and 33-34 are `ff ff` sentinels. Nothing
downstream needs any of it, so it's recorded rather than acted on.

## Device wake — the remaining blocker

The "unknown script" that makes the keyboard stream depth values is
**iLumiPC's Travel Test page**, documented as a known limitation since the
Python build. The analog interface stays silent until it's opened once per
power-on.

iLumiPC is a **WebHID** app, so the init sequence is capturable from Chrome
DevTools rather than needing Wireshark/USBPcap — the same route that
produced every protocol constant in `HidProtocolConfig`. Paste this on the
iLumiPC page *before* opening Travel Test:

```js
for (const m of ['sendReport', 'sendFeatureReport']) {
  const orig = HIDDevice.prototype[m];
  HIDDevice.prototype[m] = function (id, data) {
    console.log(m, id, [...new Uint8Array(data.buffer ?? data)]);
    return orig.apply(this, arguments);
  };
}
```

The seam is already in place: `HidProtocolConfig.WakeReports` is an empty
`byte[][]`, and `K617HidSource.Attach()` sends each entry (SetFeature
first, plain Write as fallback) right after opening. Filling in the
captured bytes is a data change, not a code change.

Uncertain until captured: whether the wake *is* a packet. It could be a
side effect of how iLumiPC opens the device. If the DevTools log comes back
empty, USBPcap is the fallback.

## The un-woken-device trap, and the guard for it

Silent tray launch + auto-start + un-woken device = every K617 key
suppressed, no analog data, no controller output, and no window to notice
it in. The keyboard simply appears dead.

Guard lives in `ModController`. "No analog reports yet" is *not* evidence
on its own — nobody may have touched the keyboard. Suppressed keystrokes
*with* zero analog reports is evidence, because it means keys are being
pressed and swallowed while the device sends nothing. `K617KeySuppressor`
gained a `SuppressedKeyCount` for this; at 3 dropped keys with no data, the
mod stops itself, restores typing, and shows a balloon naming the wake
step.

Counter kept on the concrete suppressor rather than `IKeySuppressor`, so
the interface stays the minimal start/stop contract every test fake
already implements.

## Build verification — not done in the sandbox

None of the above has been compiled. The Cowork sandbox has no .NET SDK:
`dot.net`, `packages.microsoft.com`, `builds.dotnet.microsoft.com` and
`api.nuget.org` are all blocked by the proxy, Ubuntu's `dotnet-sdk-8.0`
package needs root, and WPF can't target `net8.0-windows` on Linux anyway.
Arsh builds on Windows. Treat first-build errors as expected, not as a
sign something is deeply wrong.

## The Redragon app is a second, better capture route — 13 Aug 2026

The vendor software is at `projects/professional/keyboardcompanyapplication/`.
It is **not** Electron and **not** WebHID: it's a Qt5 native app,
`Redragon K617RGB-M.exe` (PE32 x86, 9.8 MB), and every byte it sends to the
keyboard funnels through **one exported function in `witmodSdk.dll`**, which
sits in the same folder.

```
int __cdecl CWitmodHid_HidWriteBuff(void* handle, uint8_t* buf /*64*/, const char* model);
int __cdecl CWitmodHid_HidReadBuff (void* handle, uint8_t* buf /*64*/);
```

Undecorated exports, `ret` with no operand (so cdecl, caller cleans), and the
exe imports exactly these two for all HID traffic — its only other HID import
is `HidD_GetHidGuid` for enumeration. That makes the app hookable **without
USBPcap, without a kernel driver and without admin**: attach to the export.
`witmod-hook.js` (Frida, in the app folder) does this.

### What HidWriteBuff does to the buffer

Disassembled at `witmodSdk.dll` RVA 0x84e0. It copies the caller's 64 bytes,
then overwrites two of them:

- `buf[0]` = report id. `1` normally; `6` if the model string in arg 3 is
  `"col06"`.
- `buf[1]` = a **wire command**, looked up from `buf[1]` through a 0x96-entry
  index table at RVA 0x8834 into a 51-slot jump table at RVA 0x8768.

App-level command IDs `0x2D–0x63` hit the default case, which zeroes the send
flag — those are **silently discarded and never reach the device**. Everything
else maps as:

| app cmd | wire cmd |
|---|---|
| `0x00–0x1B` and `0x64–0x7F` | `+1`  → `0x01–0x1C` |
| `0x1C–0x25` and `0x80–0x89` | `+0x24` → `0x40–0x49` |
| `0x26–0x2C` and `0x8A–0x90` | `−0x08` → `0x1E–0x24` |
| `0x91–0x95` | `−0x6C` → `0x25–0x29` |

49 distinct wire commands exist: `0x01–0x1C`, `0x1E–0x29`, `0x40–0x49`.

### The strong lead: wire command 0x21 IS the depth stream

`HidProtocolConfig.HeaderByte = 0x21` was recorded as "a constant that happens
to be on every live/summary report". It isn't a header. **0x21 is a wire
command byte, and the keyboard echoes the command it is answering** in payload
byte 0 — which is why every depth report starts with it.

So the depth reports the mod already parses are replies to wire command `0x21`
(reachable as app command `0x29` or `0x8D`). The enable packet is almost
certainly `01 21 …` with the arming bits somewhere in bytes 2–5, since byte 4
of the *reply* is the mode field (`5` = Live, `3` = Summary) the mod already
reads.

That collapses `WakeReports` from "unknown 64 bytes" to a handful of candidates
worth trying blind before capturing anything.

### Feature names, from the exe's Qt metaobject strings

The app calls this feature set "magnetic axis", not "travel test". Relevant
signals/slots, useful as anchors if the packet has to be traced statically:
`sigQueryMagneticAxisOneKeyDataBack`, `onTestRuning`,
`on_pushButton_Test_clicked`, `onCalibrationMode(int)`,
`onAllKeysCalibrationMode(int,int)`, `onSliderTriggerTravelValue(int)`,
`onMagnetsKeysApplyConfig(int,int,int,int,int)`, `theTravelNum`.
The `0x1E–0x29` wire block is the newest numbering in the table and lines up
with the newest features (magnetic axis, SnapTap, DKS/SeniorKey), so the depth
command living at `0x21` is consistent.

The exe also emits a named `qDebug` line per command — `USB serial port [Write
Sensitivity-data] Successful ...`, `[Query Keyboard DeadZoneNum]`, and so on.
Running it under DebugView gives a labelled command log alongside the hex.

### Why the CaptureHarness could not have found this

Its own doc comment says it: it records what the keyboard *sends*. The wake is
something the PC sends. No amount of listening finds it; the hook is the tool
for this half of the problem.

## Route correction — the Redragon app has no Travel Test (13 Aug 2026)

Arsh checked: the vendor app exposes no travel-test readout for this
model. Only iLumiPC does. So the capture route is **iLumiPC**, confirmed
by search to be a WebHID app at `basic.illumipc.com` — which vindicates
the original Chrome-DevTools plan the earlier notes described.

The Frida/`witmodSdk.dll` work is **not wasted**, for two reasons.

**One — the protocol facts stand.** The command-byte table, the
`buf[0]`/`buf[1]` rewrite, and above all the finding that `0x21` is a wire
command rather than a header, came out of static analysis and hold no
matter which app we capture from.

**Two — the Redragon app almost certainly can still arm the stream, under
a different name.** Its UI strings carry a full **Key Calibration** flow
(`Start calibration`, `Stop calibration`, `Key calibration mode`,
`All keys calibration`) and a signal called
`sigQueryMagneticAxisOneKeyDataBack` — "give me one key's magnetic axis
data". Calibration cannot work without reading per-key depth, so that page
is the depth stream under another label. The `Travel` page that does exist
in the binary is a *setter* (`horizontalSlider_TriggerTravel`), not a
readout, which is consistent with Arsh finding no test there.

That's now Appendix A of `capture-runbook.md`: two minutes with the
already-installed Frida hook, before committing to the browser route.

**Browser capture tooling.** `ilumipc-hook.js` (this folder) is pasted
into the DevTools console. It patches `HIDDevice.prototype.sendReport` /
`sendFeatureReport` / `open` and the `inputreport` event, and `hidDump()`
downloads the trace as a file rather than making Arsh copy truncated hex
out of the console.

**The trap that would have killed the capture.** Chrome persists WebHID
device grants per origin, so a page that has been granted the keyboard
once will reconnect *silently on load* — before any hook can be pasted.
The arming packet would go out unrecorded with no sign anything was
missed. The runbook therefore revokes the grant at
`chrome://settings/content/hidDevices` before anything else. This is
non-obvious and is the single most likely cause of a capture that "worked"
but contains nothing useful.

**Claude in Chrome is not connected** to this session, so driving the
browser directly isn't available — Arsh pastes and sends the file back.

## Frame format decoded — 13 Aug 2026, from the Redragon calibration capture

The Appendix A experiment ran. **Calibration does not stream depth** — see
below — but the capture decoded the wire format completely, which is worth
more than the wake packet on its own.

### The frame

```
byte 0   report id, always 0x01 on this interface
byte 1   command
byte 2-4 always 00 (reserved / unused)
byte 5   PAYLOAD LENGTH
byte 6+  payload
```

Confirmed by the cleanest possible case: command `0x15` replies with
length `0x20` and 32 bytes of ASCII —
`f41c6cf61ba2426b836e9fb7f091676e`, the device UUID. Length matches the
content exactly. Same holds for every other reply in the capture (len
`0x02` → 2 bytes, `0x06` → 6, `0x0c` → 12, `0x10` → 16).

Requests declare the *buffer* size rather than the used size: every `0x21`
query goes out with length `0x18` (24) and only two or three meaningful
bytes.

### `HidProtocolConfig` is mislabelled

This is the correction that matters for the mod. After the report id is
stripped, `ModeByteIndex = 4` points at **byte 5 of the frame — the
payload length**, not a mode.

So `ReportMode.Live = 5` and `Summary = 3` are not modes. They are payload
lengths:

- length 5 → payload is `subcmd, row, col, depthLo, depthHi`, which is
  exactly what `TryParse` reads at indices 6/7/8/9. The parser is
  **correct by coincidence** — right offsets, wrong reason.
- length 3 → a 3-byte payload, so the depth bytes fall outside it.

Consequence, and it is a real one: `TryParse` rejects any report whose
payload length isn't 3 or 5. If the depth stream ever emits a longer
frame — a multi-key packet, say — the mod drops it silently as
unrecognised. Renaming the constant to `PayloadLengthIndex` and gating on
"length ≥ 5" rather than "length == 5" is the honest fix.

The earlier note claiming mode-3 reports "run out to byte 35" does not
survive this framing and should be treated as suspect until re-measured.

### Command 0x21 is the magnetic-axis family, with subcommands at byte 6

| sub | meaning | evidence |
|---|---|---|
| `0x04` | device/axis info | reply `04 54 01 01 01 0a` |
| `0x05` | **per-key actuation config**, request carries `row, col` | reply `05 <flags> <a> <b> <c> <d> 00 00 00 00 <row> <col>`; default keys `00 aa aa 01 01`, five configured keys `0d 64 64 46 05` |
| `0x09`, `0x0a` | small status queries | replies `09 08`, `09 00` |
| `0x0f` | **start calibration mode** | `21 … 18 0f 00` → reply `0f 01` |
| `0x10` | **stop calibration mode** | `21 … 18 10 00` → reply `0f 00` |

The `0x05` sweep is the app enumerating every key: byte 7 walks rows 1-5,
byte 8 walks columns 0-0x0e. That confirms the `(row, col)` addressing the
mod already uses, independently.

### What calibration actually did

Between 5.4 s and 12.6 s the device pushed **unsolicited** reports — the
first unsolicited traffic seen on this interface — but they carry subcmd
`0x0f`, not depth:

```
0f 01 00 00 04
0f 01 00 00 0c
0f 01 00 00 0c 08
0f 01 00 00 0c 08 08
```

Bits accumulating across successive bytes as keys were pressed: a per-row
bitmask of which keys have been calibrated so far. Progress, not
measurement. So calibration arms an unsolicited-push mode but not the one
we want.

### Why this is still the best result available

The enable command is now a **small, enumerable space**. It is a `0x21`
subcommand — the mod's depth reports already carry command `0x21` — and
the observed subcommands are `04, 05, 09, 0a, 0f, 10`. Untouched and
plausible: `00-03, 06-08, 0b-0e, 11+`.

Two ways to close it, and they differ in risk rather than difficulty:

- **iLumiPC capture** (safe). Now trivially interpretable: look for an
  `OUT` of `01 21 00 00 00 18 <sub>` at the moment Travel Test is toggled.
  The subcommand byte is the whole answer.
- **Probe the subcommands directly** (fast, some risk). Send
  `01 21 00 00 00 18 <sub> 01` for each unknown `sub` and watch for
  unsolicited length-5 replies. Faster, but these are undocumented
  firmware commands — an unlucky one could reset or corrupt device
  settings. Not recommended before the capture, and never on the `0x40`
  block, which sits next to `CWitmodHid_FirmwareUpgrade`.

### Minor correction to the hook

The third argument to `CWitmodHid_HidWriteBuff` is the **device path**,
not a model name — `\\?\hid#vid_2e3c&pid_c365&mi_02#…`. The SDK
substring-searches it for `col06`, i.e. it is checking the HID *collection
number*, and picks report id 6 for that collection instead of 1. On
`mi_02` there is no `col`, so report id is always 1 here. The hook's
output is right; its label "model" is not.

## Web research — 13 Aug 2026

Two parallel research passes. Three findings change the plan.

### 1. iLumiPC's entire protocol is one downloadable file

`https://basic.illumipc.com/js/main.min.js`

Not hash-named — a plain filename with a `?v=<timestamp>` cache-buster, so
the URL is stable. Confirmed to exist and return content. It is the main
application bundle of a WebHID app, which means **every command the page
can send is a literal in that file**, including whatever Travel Test does.

Also present: `/js/language.min.js`, `/js/agreement.min.js`,
`/js/lottie.min.js`, `/css/main.min.css`. Directory listing is disabled
(403 on `/js/`, 404 on a control path).

**The sandbox cannot read it.** `WebFetch` decodes it as `[binary data]` —
a transport/compression issue, not obfuscation, since `lottie.min.js` (a
public library) does the same while `main.min.css` from the same host comes
back as clean text. Fetching it another way is not permitted from here. So
this is Arsh's one-click job: save the file into the folder, and the
capture becomes unnecessary.

Sibling deployments of the same app, all confirmed serving it:
`www.illumipc.com`, `drive3.kzzi.com` (KZZI), `qk75he.qwertykey.eu`
(Qwertykey QK75 HE), `tryhard-software.com`, `wraith.software`. Each names
its bundle differently — `/js/main.min.js` 404s on the siblings.

### 2. OpenRGB has a Witmod driver, and it validates the frame decode

`Controllers/WitmodKeyboardController/` in CalcProgrammer1/OpenRGB, added
May 2023. RGB-only — no magnetic or analog commands, and no `0x21`. But it
independently confirms the framing derived from the capture: byte 4 is a
packet/sequence index, **byte 5 is the payload length** (`0x36` = 54 for
packets 0-6 and `0x12` = 18 for packet 7; 7×54 + 18 = 396 = 132 LEDs × 3).

Its IDs are VID `0x0416` (Nuvoton) PID `0xC345`, against this board's
`0x2E3C`/`0xC365`. Same PID block, different silicon vendor — Witmod ships
the same firmware stack across MCUs.

**Free, zero-risk probe:** command `0x0D` is device info. Send
`01 0D 00 00 00 00` padded to 64; the reply is validated as
`[0]==0x01 && [1]==0x0D` followed by an ASCII string, documented example
`2M252,01,KB,JC,GK8120SKRGB,V1.04.04` = `[?,?,KB,?,model,firmware]`. If
that returns cleanly, the platform is positively identified and the
OpenRGB driver becomes a validated reference for the rest of the command
space.

### 3. VID 0x2E3C is ARTERY Technology, not Redragon

An MCU vendor (AT32 line). The board is an ARTERY-silicon design shipping
with the chip vendor's VID. Worth knowing: searching for "Redragon" in
HID databases will never find it.

### The push-vs-poll question, and why it's already settled here

Of the documented non-Wooting analog boards, most **poll** rather than
latch: DrunkDeer (`04 b6 03 01`), Madlions (`02 96 1c`), and Keychron
(`a9 31` = `AMC_GET_REALTIME_TRAVEL_ALL`) all re-send a read command every
frame. Only Attack Shark X68 Pro uses a one-shot enable
(`1B 01 … E3` as a feature report, disable `1B 00 … E4`).

That would have been a problem — `WakeReports` is a one-shot array, useless
against a poll-based device. **It doesn't apply here.** The 13 Aug capture
recorded 254 unsolicited reports in three seconds using the mod's own
reader, which sends nothing at all. So this board genuinely latches into a
push mode. The one-shot `WakeReports` design is correct.

Keychron's constant naming is still a useful hint: `AMC_GET_VERSION` 0x01,
`AMC_GET_REALTIME_TRAVEL` 0x30, `..._ALL` 0x31 — travel-read subcommands
sit adjacent to each other, in a block separate from config/calibration.
Expect the same clustering around this board's `0x0F`/`0x10`.

### Prior art worth citing in the README

`adapt-to-it/he-analog-gamepad` — the same project as K617Mod for an
Attack Shark X68 Pro HE. Different vendor, so the protocol does not
transfer, but it is the closest comparable and shows the technique is
established. Also `Richard121292/HallEffectAnalogMapper` (MCHOSE Jet 75 →
Xbox 360 via ViGEmBus, Python, passive `0xA0` reports, no enable command),
`wolf109909/DrunkDeer-AnalogAdapter`, and `calamity-inc/Soup`'s
`AnalogueKeyboard.cpp`, which is the single best cross-vendor reference —
it implements six brands' analog protocols in one file.

**No public reverse engineering of iLumiPC exists.** No repo, no blog, no
forum thread. Documenting this protocol is genuinely novel work, which is
worth saying plainly in the README given the resume audience.

### Flag: the firmware may already have a native Xbox360 gamepad mode

The live iLumiPC UI exposes a **gamepad mode with "Xbox360" and
"Classical" options**, alongside key travel 0-4.0 mm, dead zone, rapid
trigger, PRCS (SOCD), 5 profiles and 1K-8KHz polling.

If the K617 HE's firmware carries that, part of what K617Mod does exists in
hardware already. That does not make the project pointless — the mod adds
multi-point response curves, arbitrary key-to-control remapping, profile
management and keystroke suppression, none of which a firmware toggle
gives. But it is the first question a technical recruiter asks, and the
README should answer it before it is asked rather than after. Worth
checking on the actual device.

## Breakthrough — the depth stream's subcommand is 0x01 (13 Aug 2026)

Re-analysed `k617-capture-20260813-002848.json`, the awake capture that had
been sitting in `K617Mod.CaptureHarness/bin/Debug/net8.0/` unread at the
byte level. 254 reports. **Every single one** decodes as:

```
01 21 00 00 00 05 01 <row> <col> <depthLo> <depthHi>
^  ^           ^  ^
|  |           |  +-- subcommand 0x01  = key travel data
|  |           +----- payload length (5)
|  +----------------- command 0x21 = magnetic-axis family
+-------------------- report id
```

241 live frames at length 5, 13 summary frames at length 3 — and the
subcommand byte is `0x01` on all 254. Six distinct keys appeared,
`(2,2) (2,4) (3,2) (3,3) (3,4) (5,6)`, depth 0-340, and on the length-5
frames every byte past index 10 is zero. The payload really is exactly
five bytes.

**This is what the earlier notes were missing.** The frame decode said byte
5 was a length; it did not say what byte 6 was. Byte 6 is the subcommand,
and for depth data it is `0x01`.

### Why that pins down the wake packet

Requests in this family, from the Frida capture of the Redragon app, are
always `01 21 00 00 00 18 <sub> <args>`. And one of those subcommands is
already known to switch the device into unsolicited push mode:

- `18 0f 00` starts calibration; the device then pushes `0f` reports
  unprompted until
- `18 10 00` stops it.

The depth stream is the same mechanism with a different subcommand, and the
stream it produces is labelled `0x01`. The capture also settles push-vs-poll
beyond argument: the harness that recorded those 254 reports **never writes
anything**, so the device was genuinely free-running.

Candidates, in order:

| | frame | reasoning |
|---|---|---|
| A | `01 21 00 00 00 18 01 01` | subcommand 0x01, arg = on |
| B | `01 21 00 00 00 18 01 00` | mirrors `0f 00` exactly |
| C | `01 21 00 00 00 18 02 01` | 0x02 as start, 0x01 being data-only |
| D | `01 21 00 00 00 18 02 00` | |
| E | `01 21 00 00 00 18 03 01` | |
| F | `01 21 00 00 00 18 01 02 02` | poll one key — if this answers, the mod can poll instead of arming |

`Find-WakeCommand.ps1` (this folder) tries all six in order, listens after
each, and prints the `HidProtocolConfig` code for whichever works. Pure
PowerShell P/Invoke — no pip, no build, nothing to install. It stays inside
subcommands 0x01-0x03 and never touches 0x05 (per-key config), 0x0f/0x10
(calibration) or the 0x40 block next to firmware upgrade.

## iLumiPC's bundle: complete, and a dead end for the protocol

`main.min.js` recovered from the printed PDF: 206,865 characters, verified
by two independent extractors agreeing byte-for-byte, and **verified
complete** — all 756 entries of the obfuscator's string array are
referenced somewhere in the body, with none orphaned. A truncated print
would have left the tail strings unreferenced. Deobfuscated copy saved at
`ilumipc/main.deob.js`.

It contains exactly **one** `sendReport` call, and it is the device probe:

```
buf = new ArrayBuffer(0x3f); buf[0] = 0x0d; await dev.sendReport(0x01, buf)
```

Reply validated as `[0]==0x0d && [3]==0`, length at `[4]`, ASCII CSV from
`[5]`, field 4 = product, field 5 = firmware version. Independently
confirms the frame layout and OpenRGB's Witmod driver, third source to
agree.

Two things worth keeping from it:

**The WebHID filter names the exact interface:**
`{vendorId:0x416, productId:0x7410, usagePage:0xff1b, usage:0x91}` — the
same `FF1B:0091` collection `K617HidSource` already selects by identity.

**`route1`-`route4` are not URL routes.** They are DKS actuation points in
tenths of a millimetre (`route1/10 → "1.5mm"`). An earlier reading of them
as routing was wrong.

The bundle has no packet builders, no protocol table, and no write path
beyond the probe — this build handles connect, branding-by-hostname
(`app.zfgear.com`, `drv.4fangaming.ru`) and local profile editing in
localStorage. Whatever writes to the device is not in it. Not worth
chasing further now that the capture has given up the subcommand.

Static RE of the Redragon exe was also tried and abandoned: it queues
`QByteArray` packets into two FIFOs drained by a worker at `0x41f060` /
`0x41f118`, so there are no immediate byte stores to pattern-match. The
49 stores of `0x29`/`0x8d` in `.text` are Qt enum sequences, not buffers.

## Subcommand map of command 0x21 — complete for 0x00-0x2F (13 Aug 2026)

Four probe rounds against the live device. The keyboard rejects unknown
subcommands explicitly with `01 21 00 00 00 01 00` (length 1, payload zero,
no subcommand echo), which made clean enumeration possible.

| sub | verdict | reply / behaviour |
|---|---|---|
| `00 01 02 03 06 0c 0d` | rejected | explicit refusal |
| `04` | valid | `06 04 54 01 01 01 0a` — device/axis info |
| `05` | valid | `0c 05 …` per-key actuation config, request carries row+col |
| `07` | valid, **getter** | always `02 07 01` regardless of argument |
| `08` | valid, **mode + push** | see below |
| `09` | valid | `02 09 08` |
| `0a` | valid | `02 09 00` — writes the 0x09 state |
| `0b` | **accepted, silent, no effect** | no reply, no stream, arg 0 |
| `0e` | valid, bulk | 64 frames × payload len 0x30, **identical idle vs key-held** |
| `0f` | valid | start calibration, pushes `0f` progress |
| `10` | valid | stop calibration |
| `1c` | valid, **setter** | moves the 0x07 mode to 2 (`02 07 02`) |
| `1e` | valid | reports calibration state `18 0f 00` |
| `11-1b, 1d, 1f-2f` | silent | outside the implemented range |

**None of them starts the depth stream.**

### 0x08 is the closest thing found, and it is not depth

With argument 0, `0x08` pushes unsolicited frames as keys are tapped:

```
18 08 01 00 00 00 00 00
18 08 01 00 00 08 00 00
18 08 01 00 00 08 08 00
18 08 01 00 00 08 08 08
```

Bytes accumulating one per keypress — structurally identical to the
calibration progress pushes (`0f 01 00 00 04` → `0c` → `0c 08` → `0c 08 08`).
So `0x08` is a second calibration-style accumulator, not a depth reader.
Arguments 1 and 2 move its state byte from `01` to `04`.

### 0x0e is not live data

64 frames, 48-byte payloads, **byte-for-byte identical** whether a key is
held to the bottom or the hands are off the keyboard. It is a stored table
dump — keymap or config — not a matrix snapshot. That kills the "poll the
whole matrix" idea.

### What this rules out, and what it does not

Ruled out: every single-subcommand, single-argument request in the `0x21`
family from `0x00` to `0x2F`. That is the entire implemented range.

Not ruled out, and this is why the approach has stalled:

- **A sequence.** iLumiPC's travel test may send two or three packets in
  order — set a mode, then arm. The `0x07`/`0x1c` mode pair proves the
  device has mode state; `0x1c` was sent and left it at 2, and depth still
  did not flow, so mode 2 alone is not sufficient.
- **A different argument.** Only args `0x00`-`0x02` were walked, and only on
  a few subcommands.
- **A different command family.** The depth *replies* carry command `0x21`,
  which is why `0x21` was searched — but the enabling write could sit in
  any of the other wire commands (`0x01-0x1C`, `0x1E-0x29`, `0x40-0x49`).

The remaining space is combinatorial — twelve valid subcommands, 256
arguments each, times orderings. It is not responsibly brute-forceable, and
every attempt costs a round trip through Arsh at the keyboard.

**Conclusion: stop probing. Capture iLumiPC once, or ship the manual wake
behind the guard that already exists.**

## The wake command — found, 13 Aug

`ilumipc-capture.txt` (WebHID trace, iLumiPC session, Travel Test used once)
shows exactly one candidate, and the story around it holds together end to
end.

**The packet.** Sent once, at t=64859ms, nothing else like it anywhere in
the 1197-event trace:

```
21 00 00 00 18 02 3e 26 3e 1e 1e 1e 3e 1e 1e 3e 3e 3e 3e 00 0e
```

Command `0x21` (the magnetic-axis family, as expected), subcommand `0x02` —
a subcommand that never appeared anywhere in the earlier blind sweep with a
1-byte argument, because it was being sent there with the wrong shape. This
capture shows why: it wants a 13-byte payload, not one byte, and a 1-byte
probe of it looks identical to a rejection.

**Why this is the one, not just "an" outgoing packet near the right time:**

- It is the *only* `sub=0x02` in the whole capture.
- Depth reports for row/col `02 02` start 6.5 seconds after it, with *zero*
  other outgoing packets in that gap — nothing else could have caused it.
- The 6.5s gap stops looking like a problem once you read it as "time for
  Arsh to notice the packet went out and press the key" rather than
  "device-internal delay." The depth report's value visibly climbs
  (`0x51` → `0x84` → …) exactly like a key being pressed down.
- Depth reports for **seven other row/col pairs** (`02 00`, `03 00`, `03 02`,
  `03 03`, `03 04`, `04 05`, `05 00`) show up later in the same trace, and
  `sub=0x02` was never sent again. One packet, sent once, and the *entire
  keyboard* started reporting depth — not just the key that was selected in
  the Travel Test UI when it was sent. That is exactly the shape the mod
  needs: a single global arm, not a per-key one.

**What the payload probably is.** The 13 bytes (`3e 26 3e 1e 1e 1e 3e 1e 1e
3e 3e 3e 3e`) sit where the Travel Test UI's per-key trigger-point sliders
would write their values — plausible, not confirmed. It has not been tested
whether those specific 13 bytes matter or whether the command arms
streaming regardless of what is in them. Untested either way.

**Confidence: high, not certain.** This is inference from a single
observed session, not a controlled test. The correlation is about as clean
as a passive capture can produce — one candidate, one clean causal story,
corroborated by unrelated keys later streaming unprompted — but it has not
been fired in isolation yet to rule out coincidence.

**Wire form**, report ID restored (WebHID strips it; the byte above is
`d[0]=0x21`, on the wire it is `r[1]=0x21` after `r[0]=0x01`):

```
01 21 00 00 00 18 02 3e 26 3e 1e 1e 1e 3e 1e 1e 3e 3e 3e 3e 00 0e
[padded to 64 bytes with 0x00]
```

## Correction — the wake command isn't 0x02, 13 Aug

`Test-WakePacket.ps1`, sending `sub=0x02` alone with the exact 13-byte
payload from the capture, got the same reply back both times:

```
21 00 00 00 01 00
```

That reply is identical to the generic rejection signature from the
Map-Subcommands sweep. Sent in isolation, with nothing else around it, it
did not arm the stream — 8 seconds of key-mashing afterward produced 224
frames and zero depth reports. The correlation in the capture was real, but
it was not causal. The single-candidate theory is dead.

**Re-reading the capture with that ruled out.** The full outgoing sequence,
in order:

1. `~3.9s`–`4.0s` — 29× `cmd=0xd` device-info probes (retries, not
   relevant to us).
2. `~5.1s`–`12.8s` — 10× `cmd=0x19` probes roughly once a second, plus one
   each of `cmd=0x1, 0xe, 0x22, 0x24×2, 0x18×2, 0xa, 0x2, 0x7, 0x8`. This
   reads as iLumiPC trying several *other* Redragon protocol families
   before settling on this device's. Not ours.
3. `~13.0s` — `cmd=0x21 sub=0x4` (device/axis info — matches the earlier
   map).
4. `~13.0s`–`13.22s` — 61× `cmd=0x21 sub=0x5`, walking every row/col in the
   key matrix (`01 00` through `05 0c`). This is a **getter**, not a
   setter — the reply echoes the row/col back with existing config
   (`aa aa 01 01 ...`). It's iLumiPC reading the whole per-key actuation
   table into its UI, not arming anything.
5. `13223ms` — `cmd=0x21 sub=0x9` (accumulator getter, matches the map).
6. `13255ms` — `cmd=0x21 sub=0x11`, **with the length byte set to 0x00**,
   not the `0x18` every earlier probe (ours and iLumiPC's own) used
   elsewhere. No reply. This exact shape — `sub=0x11`, zero length, zero
   payload — was never tested in isolation. Every earlier probe of `0x11`
   forced `len=0x18` with a padding byte, which may not be the shape this
   subcommand expects.
7. **51 seconds of nothing** — Arsh navigating to Travel Test.
8. `64859ms` — the now-disproven `sub=0x02`.
9. `71443ms` — depth starts.

**What's left, honestly.** One untested shape (`sub=0x11`, len=0x00, no
payload) sent 51 seconds before depth started, separated by total silence.
That gap makes it a weaker candidate than `0x02` was — correlation in time
is now the *only* thing it has going for it, and the last candidate that
had that much lost when tested alone. Everything else in the sequence is
either a getter (0x05, 0x09) or unrelated protocol probing (0xd, 0x19, and
the rest).

No confident candidate remains. The three honest paths from here are in
`TASKS.md`.

## Wake command confirmed on the MVP, 13 Aug

The `sub=0x02` candidate above was retested end-to-end on the Python
build via `test_wake_mvp.py` (new script, same folder as `hid_reader.py`)
- same structure as `Test-WakePacket.ps1`: baseline check confirming the
device silent first, full reboot beforehand, packet sent alone with
nothing before or after, then a keypress window. Unlike the isolated
PowerShell run, this one passed: depth reports followed the keypress
after the packet alone, from a genuinely clean baseline. Arsh confirmed
it on his machine.

This resolves the "Correction - the wake command isn't 0x02" entry
above. The earlier disproof still stands as a real result on its own
data (that specific isolated run really did fail), but the packet itself
is now independently confirmed working via a second controlled test.
Two isolated tests of the same candidate disagreeing is unusual enough
to flag rather than paper over - if the wake step ever seems flaky again
(intermittent, works-then-doesn't), this is the first thing to revisit.

Implemented in both builds:
- Python: `hid_reader.py`, `_WAKE_REPORT_ID` / `_WAKE_PAYLOAD`, sent in
  `K617Reader.__init__` via `_wake_analog_interface()`.
- C#: `HidProtocolConfig.WakeReports`, sent in `K617HidSource.Attach()`
  via the existing `TrySendWakeReports()` - no code changes needed there,
  exactly as designed when that seam was built.

The manual "open iLumiPC once per reboot" step is no longer required on
either build. `TASKS.md`'s "Capture the wake sequence" item is closed.

## Start/stop button + persisted mode, 13 Aug

Built the in-window mod switch (`TASKS.md`'s "in-window ON/OFF toggle"
item) per Arsh's spec: the app remembers whichever mode - on or off - it
was last explicitly put into, and opens in that mode on launch rather
than always auto-starting. Off means a completely normal keyboard
(suppression and the HID read loop both stopped); on means suppression +
controller output, same as today, and the tray reflects it either way.

Implementation, in the order it was built:

- `IProfileStore` / `JsonProfileStore`: added `GetLastModeActive()` /
  `SetLastModeActive(bool)`, stored on `AppSettingsDocument.LastModeActive`
  (nullable bool - null means "never set", read as true/on, so a fresh
  install still auto-starts on first launch same as before this existed).
- Found and fixed a bug while doing this: `SetLastActiveProfileName` and
  the new `SetLastModeActive` would each construct a brand new
  `AppSettingsDocument` and overwrite the whole file - meaning the two
  settings would silently clobber each other depending on save order.
  Refactored both to load-modify-save through the same file. Added a
  regression test (`SettingActiveProfileAndModeActive_BothPersistIndependently`)
  so this can't silently reappear if a third setting gets added later.
- `ModController`: gained a constructor (previously implicit/parameterless)
  that owns its own `IProfileStore` for settings, separate from the
  per-Start() profile store `Start()` already constructs locally - kept
  them separate rather than risk touching the working Start() logic.
  `Start()`/`Stop()` now persist the mode, but only on the paths a person
  actually triggers them from - not from the sleeping-device watchdog
  (calls `StopInternal()` + `SetStatus()` directly, same as before) and
  not from `Dispose()` on normal app exit. A crash-triggered `Stop()`
  (via `App`'s `DispatcherUnhandledException` handler) DOES persist off -
  judged as the safer default after an unexplained crash rather than
  risking a suppression crash-loop, not specially exempted.
- `App.xaml.cs`: startup now calls `_controller.ShouldStartOnLaunch()`
  before auto-starting, instead of always starting unconditionally.
- `TrayIcon.cs`: `ShowWindow()` changed from a static method to an
  instance method so it can hand its `ModController` into `MainWindow`'s
  constructor - the one piece MainWindow was missing to become a second
  view over the same controller the tray already uses. Nothing else in
  this file changed; its own status rendering was left untouched.
- `MainWindow.xaml` / `.xaml.cs`: new header bar above the nav/content
  split (app-wide state, not part of any one settings page) with a
  hand-templated pill `ToggleButton` (no native WPF switch control) plus
  a status dot and text, following the exact same StatusChanged ->
  dispatcher-marshal -> refresh pattern `TrayIcon` already used. The
  status-text switch expression is intentionally duplicated from
  `TrayIcon.Refresh()` rather than shared - WPF and WinForms use
  different `Color` types, and sharing across two views wasn't worth an
  abstraction for two call sites. Worth revisiting if a third view needs
  the same text.

**Not build-verified beyond `dotnet build` succeeding** - the switch
itself, and the persisted-mode-on-relaunch behaviour, still need a real
smoke test on Windows: toggle off, relaunch, confirm it opens off (and
the keyboard types normally); toggle on, relaunch, confirm it opens on.

## Profiles wired into the running mod, 13 Aug

### What was actually broken

Profiles were half-connected, not disconnected. `ModController.Start()`
already read `ProfileSession.Current.SelectedProfileName`, loaded that
profile and built both the key map and the tuning from it — so the
profile in the dropdown was genuinely the profile the mod ran, at the
moment it started. Nothing hardcoded was being read there;
`InputTuningConfig` had already been stripped to raw depth calibration.

The gaps were all after `Start()`:

- **Nothing could reach a running pipeline.** `ModController` built its
  `TuningSource` as a local inside `Start()` and dropped the reference on
  return, so it physically could not swap tuning later. Saving a curve
  did nothing until the mod was toggled off and on.
- **Switching profile mid-run did nothing.** `ProfileSession` raised
  `ProfileReloaded`, but only the two settings pages listened.
- **Key bindings could not hot-swap at all.** `InputState` read an
  `IKeyMap` once in its constructor and cached the analog key names in
  readonly fields. Curves had `ITuningSource` built for live swapping;
  the mapping half had no equivalent.
- **Profile choice was only remembered on Apply.**
  `SetLastActiveProfileName` was called inside `Apply()`, which returns
  early for read-only profiles — so selecting a profile and closing lost
  the choice, and `Default` could never be the remembered one.

### Decisions

**Key bindings hot-swap, same as curves.** The alternative was rebuilding
the pipeline on remap — about 30 lines instead of 150 — but it drops
suppression and the virtual pad for a moment, which the game sees as the
controller disconnecting and reconnecting, and it briefly lets a stray
keystroke through to the game. Rejected against the "save applies live"
decision from 12 Aug.

**Selecting a profile takes effect immediately.** Apply/Revert now only
govern unsaved edits within a profile, which is what those buttons
already meant. Also removes the special case where `Default` could never
be made active, since that path no longer goes through a save.

### How it works

`KeyBindingSet` (Core/State) is the mapping half of a profile in the
shape the pipeline consumes — the exact counterpart of `ProfileTuning`.
Immutable; changing bindings means building a new one and swapping it.
It holds analog bindings as an action → key dictionary rather than a
field per control, so `InputState` keeps sole ownership of which action
ids are analog and adding a seventh analog control touches one file.

The two halves swap by deliberately different mechanisms:

- **Tuning** stays lock-free. Read once per tick, nothing else touches
  it, so a volatile reference assignment is enough.
- **Bindings** go under the lock `Update()` and `Snapshot()` already
  share, because they are read together with the mutable depth values
  they index into and the two have to change as one thing. That lock is
  taken 64 times a second regardless; the swap happens when a person
  clicks something.

`AppOrchestrator.ApplyKeyMap` updates the position → key lookup on its
own `_keyMap` field (now `volatile`, read from the HID thread) and then
calls `InputState.ApplyBindings`. That order is deliberate: a report
arriving between the two resolves to a key name the old bindings don't
recognise and is dropped — one lost reading at 64Hz. The reverse order
could route a reading from the old physical position onto a new action,
briefly moving the wrong control.

`InputState.ApplyBindings` carries depth readings across for keys that
stay bound, so a key held down through a profile change doesn't
spuriously release — a handbrake letting go mid-corner would get blamed
on the game. Keys that are no longer bound are dropped, which stops a
stale depth reappearing if that key is rebound later.

### Wiring

`ModController.ApplyProfile(name)` is the single entry point. Running →
swaps both halves in place. Stopped → records and persists the choice for
the next `Start()`. Returns `string?` rather than throwing: a profile
that won't load leaves the mod running correctly on the previous one,
which isn't an error state for the mod, only for whatever asked.

`ProfileSession` raises `LiveProfileChanged` on selection change and
after a successful save. `App.xaml.cs` is the only place the two are
connected — the session's job is editing profiles, the controller's is
deciding what a change means for a pipeline that may or may not be
running, and keeping the call one-way leaves the settings pages testable
with no HID device or ViGEm driver in reach.

`ModController` no longer reaches into `ProfileSession.Current`, and no
longer creates a fresh `JsonProfileStore` and re-bootstraps on every
`Start()`. Bootstrap happens once in its constructor. `ProfileSession`
also bootstraps in its own constructor; both calls are idempotent, and
keeping ModController's means the tray-only path works without the
window ever being constructed.

`MainWindow` gained a profile selector in the header beside the ON/OFF
switch. Which profile is live is app-wide state, the same as the mode.
The two page-level combos still work — all three are views of one
`ProfileSession`.

### Known, deliberately left

Switching profile with unsaved edits still discards them silently, as it
did before. It deserves a confirmation prompt, but that is a bigger
change than it looks: "cancel" has to put the combo box back without
re-entering the setter and firing another change.

### Verification

No .NET SDK ships in the sandbox, and NuGet is blocked by the proxy, so
`dotnet test` cannot run there. The SDK itself installed from Ubuntu's
archive, which was enough to type-check all of `K617Mod.Core` (excluding
the two files needing HidSharp/ViGEm, both untouched) plus
`ModController.cs` and `ProfileSession.cs` against real Core sources —
clean, zero warnings.

The new xunit tests were also re-implemented as a plain console runner
against the same Core sources, since xunit itself can't be restored. All
22 assertions pass, including a 20,000-iteration swap loop running
against a concurrent `Snapshot()` reader. Everything WPF-facing
(`App.xaml.cs`, `MainWindow.xaml`) is unverified — it needs a Windows
build.
