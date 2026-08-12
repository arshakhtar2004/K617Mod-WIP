# Part 6 - Profile / Persistence Module

## What this is

Saves and loads named profiles (tuning values + key mapping, bundled
together) as JSON files, plus a small app-settings file tracking which
profile was active last. No hardware, no driver - pure file I/O, fully
unit-testable with a temp directory.

## Structure

```
K617Mod.Core/
  Persistence/
    IProfileStore.cs           - the contract; Part 7 (UI) and Part 8 depend on this
    JsonProfileStore.cs          - real implementation, one JSON file per profile
    ProfileDocument.cs             - wire format for one profile
    AppSettingsDocument.cs           - wire format for app-wide settings (currently just last-active-profile)
    DefaultProfiles.cs                 - built-in "Typing" starter profile

  Mapping/
    KeyMapLoader.cs             - MODIFIED (see below) - two new methods added, nothing removed

K617Mod.Core.Tests/
  Persistence/
    JsonProfileStoreTests.cs   - hermetic, temp directory per test
    DefaultProfilesTests.cs     - tests the built-in Typing profile

  Mapping/
    KeyMapLoaderDocumentTests.cs - confirms the Part 2 modification didn't change existing behavior
```

## Important: one existing file was modified, not just added to

`K617Mod.Core/Mapping/KeyMapLoader.cs` (from Part 2) has two new public
methods added: `LoadDocumentFromFile` and `LoadDocumentFromJson`, plus
`FromDocument` was pulled out as its own reusable method instead of
being inlined inside `LoadFromJson`. **`LoadFromFile` and `LoadFromJson`
behave exactly as before** - this is a pure addition, confirmed by
`KeyMapLoaderDocumentTests.FromDocument_ProducesSameResultAsTheOriginal
LoadFromFile`, not a rewrite of anything Part 2 already proved correct.

You'll need to **replace your existing `KeyMapLoader.cs`** with the
version in this delivery, not just add new files alongside it.

## Why it's structured this way

- **A profile bundles tuning + mapping together, reusing Part 2's
  `KeyMapDocument` directly** rather than inventing a second copy of the
  same shape. A profile genuinely is "how keys behave for this game,"
  and both halves of that were already modeled somewhere - this just
  composes them.
- **No index/manifest file listing which profiles exist.** The profiles
  folder itself is the source of truth - `ListProfileNames()` just lists
  `*.json` files there. One less thing that could drift out of sync with
  reality.
- **Only "Typing" is a hardcoded default, not "FH6."** Explained in
  `DefaultProfiles.cs`'s comments - embedding a second copy of the
  17-key FH6 mapping here would duplicate `keymapping.default.json`'s
  data a second time. Bootstrapping a first-run "FH6" profile from that
  real file is left as Part 8's job, once there's an orchestrator to
  decide "what should exist the first time the whole app runs."
- **Root storage directory is passed in, not hardcoded.** Tests point it
  at a temp folder (and clean up after themselves via `IDisposable`);
  the real app will point it at the user's AppData folder once Part 8
  wires it up. `JsonProfileStore` itself doesn't know or care which.

## Build & test steps

No hardware, no driver, no admin rights needed for any of this:
```
cd K617Mod.Core.Tests
dotnet test
```
Expect the previous 45 plus roughly 18 more from this part (profile
store tests, default profile tests, and the KeyMapLoader regression
check) - somewhere around 63 total. If the exact number looks
meaningfully different, paste the output rather than guessing why.

## Next part

With this in place, a lot of what you listed while planning your trip
becomes concrete: named profiles are now a real, saved thing, and each
one already carries its own tuning values - which is most of what a
curve-editing UI or a profile-switching UI will bind directly to. Next
up whenever you're ready: either the **Simulated Input Source** (to
unlock building/watching the UI without hardware) or **Part 7 (UI)**
itself, your call.
