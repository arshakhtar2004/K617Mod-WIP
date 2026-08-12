# Part 5 - Key Suppression Module

## What this is

Blocks every key on the K617 HE from reaching Windows/games while the
app is running, via the Interception kernel driver, while leaving every
other keyboard on the system completely untouched. Racing input comes
from the raw HID stream (Part 1), not from keystrokes, so nothing is
lost by blocking 100% of the K617's normal typing.

## Structure

```
K617Mod.Core/
  Suppression/
    IKeySuppressor.cs           - the contract; Part 8 (orchestrator) depends on this
    K617KeySuppressor.cs          - the real implementation
    HardwareIdMatcher.cs            - PURE LOGIC: is this hardware ID the K617? (testable)
    Native/
      NativeMethods.cs              - raw Win32 P/Invoke declarations
      InterceptionDevice.cs           - one device file (\\.\interceptionNN)
      InterceptionContext.cs            - manages all 20 device files
      KeyStroke.cs                        - struct matching the driver's wire format
      KeyFilter.cs                          - filter flags enum
      DualHandle.cs                           - IOCTL_SET_EVENT buffer shape
      StructMarshal.cs                          - struct<->byte[] helpers

K617Mod.Core.Tests/
  Suppression/
    HardwareIdMatcherTests.cs   - the ONLY automated tests in this part - pure string logic

K617Mod.SuppressionHarness/
  Program.cs                    - attaches real suppression, you verify it by typing
```

## Important honesty note before you test this

This is the most low-level code in the project so far - it talks
directly to the Interception kernel driver via raw `CreateFileW` /
`DeviceIoControl` calls, ported field-for-field from the Python build's
`interception.py` (constants, struct layouts, IOCTL codes all copied
over exactly). Unlike HidSharp or ViGEm.NET, there's no NuGet package
abstracting this away - which means there's also no way for me to
compile-check it against a real library from this environment. The
struct layouts and IOCTL codes are a faithful port of code that was
already proven working in the Python version, so the *logic* should be
sound - but this genuinely cannot be called confirmed until the harness
runs successfully on your machine. Please actually run the harness step
below rather than assuming this one "should just work."

## Two prerequisites before the harness will do anything

1. **Interception driver installed.** Download the installer, run
   `install-interception.exe /install` as Administrator, then **reboot** -
   the driver won't attach without a reboot after install.
2. **Run the harness itself as Administrator.** Low-level device access
   like this typically gets denied silently or with an access-denied
   error otherwise - if `Start()` throws something unexpected, this is
   the first thing to check.

## Why it's structured this way

- **`HardwareIdMatcher` is deliberately its own file, separate from
  everything driver-related.** It's pure string matching - no P/Invoke,
  no device handles - which is exactly what makes it the one piece of
  this part that gets real automated tests instead of a manual harness.
- **`Native/` is a self-contained low-level wrapper**, not specific to
  the K617 at all - `K617KeySuppressor` is the only file that knows
  *which* device to drop. If this project ever needed to suppress a
  second device too, `Native/` wouldn't need to change.
- **One deliberate exception to the "duplicate constants" pattern from
  Part 3:** `HardwareIdMatcher` references `Hid.HidProtocolConfig`'s
  VendorId/ProductId directly instead of copying them a third time.
  Reasoning's in that file's comments - device identity is a single
  fact, not a tunable judgment call, so duplicating it further would
  just create a way for values to silently drift apart.

## Build & test steps

**Automated - no driver, no admin rights needed:**
```
cd K617Mod.Core.Tests
dotnet test
```
Only exercises `HardwareIdMatcher`. Expect the previous 39 plus 6 more
from this part.

**Hardware harness - needs both prerequisites above:**
1. Open a terminal **as Administrator** (right-click Terminal/PowerShell -> "Run as administrator").
2. ```
   cd K617Mod.SuppressionHarness
   dotnet run
   ```
3. Once it says "Attached," open Notepad.
4. Type on the K617 HE - **nothing should appear**.
5. Type on your laptop's built-in keyboard or any other keyboard -
   should type completely normally.
6. `Ctrl+C` - K617 typing should return to normal immediately.

If step 4 doesn't suppress anything, or step 5 also gets blocked
(wrong - only the K617 should be blocked), tell me exactly what
happened rather than me guessing at another fix blind - this is
exactly the kind of module where real behavior needs to drive the next
change, not another assumption from this end.

## Next part

Part 6 (Profile/Persistence) is next - pure JSON read/write of saved
settings, no hardware, no driver, fully unit-testable on its own.
