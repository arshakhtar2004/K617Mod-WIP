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
REM  Roughly 60-90 seconds and ~150MB of intermediate files the first
REM  time; faster afterwards.
REM ---------------------------------------------------------------

set "HERE=%~dp0"
set "PROJECT=%HERE%K617Mod.Ui\K617Mod.Ui.csproj"
set "OUT=%HERE%dist"

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
