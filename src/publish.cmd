@echo off
setlocal

REM ---------------------------------------------------------------
REM  Builds K617Mod.exe: one self-contained file you can double-click,
REM  copy to another machine, or pin to the taskbar. No .NET runtime
REM  needed on the machine that runs it.
REM
REM  Run this from anywhere - it works out its own location.
REM  Output lands in  dist\K617Mod.exe  next to this script.
REM
REM  Deletes bin\/obj\ and rebuilds from clean every time (see below for
REM  why) - roughly 60-120 seconds and ~150MB of intermediate files.
REM ---------------------------------------------------------------

set "HERE=%~dp0"
set "PROJECT=%HERE%K617Mod.Ui\K617Mod.Ui.csproj"
set "OUT=%HERE%dist"

REM  Clear intermediate build state before publishing.
REM
REM  The first two runs of this script failed with MSB3094 ("SourceFiles
REM  refers to 2 item(s), DestinationFiles refers to 1") straight after a
REM  `dotnet test`. The third run - same project, same flags, nothing
REM  edited - succeeded with no changes at all. That pattern (fails,
REM  fails, then succeeds untouched) is the signature of a stale
REM  incremental-build cache: `dotnet test` builds Debug with no RID,
REM  this script publishes Release for win-x64 self-contained, and the
REM  two leave conflicting item lists in the same obj\ folder for
REM  MSBuild's incremental-build bookkeeping to trip over. Deleting bin\
REM  and obj\ first removes that stale state so every publish starts
REM  from the same place `dotnet test` doesn't touch.
echo Clearing previous build output...
if exist "%HERE%K617Mod.Core\bin" rmdir /s /q "%HERE%K617Mod.Core\bin"
if exist "%HERE%K617Mod.Core\obj" rmdir /s /q "%HERE%K617Mod.Core\obj"
if exist "%HERE%K617Mod.Ui\bin"   rmdir /s /q "%HERE%K617Mod.Ui\bin"
if exist "%HERE%K617Mod.Ui\obj"   rmdir /s /q "%HERE%K617Mod.Ui\obj"

echo.
echo Publishing K617 HE Racing Mod...
echo   project: %PROJECT%
echo   output:  %OUT%
echo.

REM  -r win-x64            the only architecture this targets
REM  --self-contained      bundle the .NET runtime, so no install is needed
REM  PublishSingleFile     one .exe rather than a folder of DLLs
REM  IncludeNativeLibraries...  required for WPF: its native rendering
REM                        libraries cannot load from inside the bundle and
REM                        are unpacked to a temp folder on first run
REM  EnableCompression...  roughly halves the size, costs ~1s of startup
REM  PublishTrimmed=false  deliberate. WPF uses reflection throughout, and
REM                        a trimmed build compiles fine then fails at
REM                        runtime on whichever screen touched the trimmed
REM                        type. Not worth the size saving.

dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:PublishTrimmed=false ^
  -p:DebugType=none ^
  -o "%OUT%"

if errorlevel 1 (
  echo.
  echo PUBLISH FAILED - see the errors above.
  exit /b 1
)

echo.
echo Done. The app is at:
echo   %OUT%\K617Mod.exe
echo.
echo Double-click it. Windows will ask for administrator - that is
echo expected, the key suppressor needs it. The window does not open on
echo launch by design: look for the coloured dot in the system tray.
echo.

endlocal
