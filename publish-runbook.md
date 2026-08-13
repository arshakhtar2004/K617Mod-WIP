# Building K617Mod.exe

Turns the project into one self-contained `K617Mod.exe` you can
double-click, copy to another machine, or pin to the taskbar. The
machine that *runs* it needs no .NET installed; the machine that
*builds* it does.

## Before you start

Confirm each of these rather than doing anything yet:

- `dotnet --version` in PowerShell prints 8.x or higher. If it doesn't,
  the .NET 8 SDK isn't installed and nothing below will work.
- You're on the branch you actually want to ship. `git log --oneline -1`
  in the repo should show the commit you expect.
- About 1GB free on the drive holding the repo. The publish writes
  ~150MB of output and a similar amount of intermediates.

Two things the built exe still needs on whatever machine runs it, and
neither is bundled because both are drivers:

- **ViGEmBus**, for the virtual controller.
- **Interception**, for key suppression.

Without ViGEmBus the app shows "Could not reach the ViGEmBus driver".
Without Interception it runs but the keyboard also types into the game.

---

## Step 1 — Run the publish script

**Do:** open PowerShell in the repo and run:

```powershell
.\src\publish.cmd
```

**Why:** it wraps a long `dotnet publish` line with the flags that make
the output a single self-contained file. Running `dotnet publish` bare
produces a folder of ~200 DLLs instead, which still works but isn't
what "here is an exe" means.

**Check:** the last lines print `Done.` and a path ending
`dist\K617Mod.exe`. `dir src\dist` shows `K617Mod.exe` at roughly
70-90MB.

**If not:**
- *"'dotnet' is not recognized"* — the SDK isn't on PATH for this shell.
  Close PowerShell, reopen it, try again; if it still fails, the SDK
  isn't installed.
- *NuGet restore errors* — the machine can't reach nuget.org. The
  HidSharp and ViGEm packages have to come down at least once.
- *"The process cannot access the file ... K617Mod.exe"* — a previous
  copy is still running. Right-click its tray icon, Quit, then re-run.

## Step 2 — Launch it

**Do:** double-click `src\dist\K617Mod.exe`.

**Why:** this is the whole point of step 1 — proving the artefact works
on its own, not just under a debugger.

**Check:** a UAC prompt appears, you accept, and then **no window
opens**. A small coloured dot appears in the system tray instead. That
is correct behaviour, not a failure: the app is tray-first by design.
Green means running with suppression active.

**If not:**
- *"Windows protected your PC" (SmartScreen)* — expected for an
  unsigned exe with no download reputation. Click **More info** →
  **Run anyway**. It will keep happening on every new build until the
  exe is code-signed, which needs a certificate.
- *No UAC prompt and no tray icon* — the app is probably already
  running from an earlier launch; a mutex stops a second copy. Check
  the tray, including the hidden-icons chevron.
- *UAC prompt, then nothing at all* — check `%AppData%\K617Mod` exists.
  If it doesn't, the app failed before it could create its profiles.

## Step 3 — Confirm the mod itself works

**Do:** click the tray icon to open the config window, then check the
header.

**Why:** the tray dot tells you it launched; the header tells you
whether the two drivers actually attached, which is the part that
silently degrades.

**Check:** the switch reads on, and the status text says *"Running -
K617 keys are controller input only."*

**If not:**
- *"Running, but suppression FAILED - keys also type"* — Interception
  isn't installed, or the exe wasn't elevated. It asks for elevation
  automatically, so this is almost always the driver.
- *"Stopped - keyboard needs waking"* — the analog interface didn't
  respond. The automatic wake packet is sent on connect, so this
  usually means the keyboard was reconnected after the app started.
  Toggle the mod off and on.
- *"Could not start."* — read the detail text; it names ViGEm or
  Interception explicitly when either is the cause.

---

## End state

`src\dist\K617Mod.exe` exists, launches to a tray icon, and reports
"Running - K617 keys are controller input only". You can copy that one
file anywhere on the machine, or to another machine that has the two
drivers, and it behaves the same.

`src\dist\` is inside the `bin`/`obj` ignore rules' spirit but not
matched by them — it is deliberately **not** committed. Publish output
doesn't belong in git; build it when you need it.

## Known rough edges

- **Every launch shows a UAC prompt.** Unavoidable while suppression
  needs the Interception driver. It also means the app can't be started
  from the usual `HKCU\...\Run` registry key, so a future "start with
  Windows" feature needs a Task Scheduler task with highest privileges.
- **SmartScreen warns on every new build.** Only code signing removes
  it.
- **First launch after publish is slower** than later ones. WPF's native
  rendering libraries can't load from inside a single-file bundle, so
  they're unpacked to a temp folder once.
