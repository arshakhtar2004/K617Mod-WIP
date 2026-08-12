# Part 1 - HID Interface Module

## What this is

The lowest-level piece of the pipeline: opens the K617 HE's analog HID
interface and yields raw `(row, col, depth, mode)` reports. It has **no
knowledge** of key names, controller actions, ViGEm, or the UI - on
purpose, so it can be built, tested, and later modified without touching
or breaking anything else.

## Structure

```
K617Mod.Core/
  Hid/
    IHidKeySource.cs        - the contract everything downstream depends on
    RawKeyReport.cs          - raw event data (row, col, depth, mode, timestamp)
    ReportMode.cs             - Live / Summary enum
    HidProtocolConfig.cs      - protocol constants (VID/PID, byte offsets, ranges)
    K617HidSource.cs          - real device implementation via HidSharp

K617Mod.DevHarness/
  Program.cs                  - console app, proves Part 1 works alone
```

## Why it's structured this way

- `IHidKeySource` is the only thing any future module should reference.
  Not `K617HidSource` directly. That's what makes it swappable later for
  a recorded-replay source without touching mapping/state/output/UI code.
- `HidProtocolConfig` holds *only* protocol-level facts (byte offsets,
  depth range). It deliberately does not know what a "key name" is -
  that mapping is a separate module, kept independent of this one.

## Build & test steps

1. Open `K617Mod.sln`... actually there's no `.sln` yet - either create
   one in Visual Studio ("Add Existing Project" for both folders) or run
   from the CLI:
   ```
   cd K617Mod.DevHarness
   dotnet run
   ```
2. `dotnet restore` needs to pull `HidSharp` from NuGet - requires normal
   internet access on the machine you build this on.
3. Plug in the K617 HE. Remember the existing "device wake" step from the
   Python build still applies here - open iLumiPC's Travel Test page once
   per reboot before running this, until that gets ported into the app
   itself later.
4. Run the harness, press keys, confirm you see live rows printing with
   sane row/col/depth values matching what you already know from the
   Python capture (W = row 2, col 2, etc).

## Known gap to resolve during that first real build

`K617HidSource.Start()` currently takes the *first* HID interface
matching the K617's VID/PID, rather than filtering by usage page like the
Python build did. HidSharp can expose usage-page info via its
report-descriptor API, but the exact call differs enough across package
versions that it wasn't safe to hard-code without testing against the
real installed version. If the first-match device turns out to be the
wrong interface (reports come back empty or garbled), that's the fix to
make - filter `candidates` by usage page before opening one, the same way
`hid_reader.py`'s `find_device_path()` did.

## Next part

Part 2 (Key Mapping module) can be built and fully unit-tested completely
independently of this one - it only needs `(row, col)` integers as input,
which can just be hand-written test values. It doesn't need the real
keyboard, HidSharp, or this module at all until the orchestrator wires
them together at the very end.
