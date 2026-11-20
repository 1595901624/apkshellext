@echo off
rem Post-build steps for ApkShellext2, invoked from ApkShellext2.csproj:
rem   call "$(ProjectDir)Resources\postbuild.cmd" "$(TargetDir)" "$(ProjectDir)" [Configuration]
rem Kept as a real .cmd file on purpose: PostBuildEvent inline batch text goes
rem through an MSBuild-generated temp .cmd where line continuations, %% escaping
rem and block parsing are fragile. A plain file avoids all of that.
rem
rem This script ONLY does 7z packaging. The publish layout itself
rem (install*.bat, restart_explorer.bat, libwebp_*.dll, debug.bat) is assembled
rem by MSBuild: those files are Content items with CopyToOutputDirectory in
rem ApkShellext2.csproj, copied by CopyFilesToOutputDirectory during the build,
rem so $(TargetDir) is already a complete publish folder when this runs.
setlocal EnableDelayedExpansion
set "TDIR=%~1"
set "PDIR=%~2"
set "CONF=%~3"
if "%CONF%"=="" set "CONF=Debug"
if not "%CONF%"=="Release" exit /b 0

cd /d "%TDIR%"

rem debug.bat is a dev helper; MSBuild copies it in Debug builds only. Delete a
rem stale one if it lingers here so the Release *.bat wildcard does not package it.
del /q "%TDIR%debug.bat" 2>nul

rem Locate 7z.exe without hardcoding a machine path:
rem 1) %ProgramFiles%\7-Zip and %ProgramFiles(x86)%\7-Zip
rem 2) HKLM\SOFTWARE\7-Zip InstallPath/Path in BOTH registry views - the classic
rem    VS Bin\MSBuild.exe is 32-bit, so a bare query here would run in the WOW64
rem    (32-bit) view and miss a 64-bit 7-Zip install; /reg:64|/reg:32 forces each
rem    view explicitly
rem 3) PATH
set "Z7=%ProgramFiles%\7-Zip\7z.exe"
if exist "!Z7!" goto :z7found
set "Z7=%ProgramFiles(x86)%\7-Zip\7z.exe"
if exist "!Z7!" goto :z7found
for %%V in (64 32) do (
  for %%N in (InstallPath Path) do (
    for /f "usebackq tokens=2,*" %%i in (`reg query "HKLM\SOFTWARE\7-Zip" /v %%N /reg:%%V 2^>nul ^| find /i "%%N"`) do set "Z7=%%j\7z.exe"
    if exist "!Z7!" goto :z7found
  )
)
for /f "usebackq delims=" %%i in (`where 7z 2^>nul`) do (set "Z7=%%i" & goto :z7found)
:z7found
if not exist "!Z7!" (
  echo WARNING: 7-Zip not found - checked ProgramFiles^(x86^), registry HKLM\SOFTWARE\7-Zip ^(both views^) and PATH. Skipping 7z packaging.
  exit /b 0
)
set "Z7=!Z7!"

REM Rebuild archives from scratch: "7z a" only updates/adds, so a stale
REM archive would silently ship files that no longer exist on disk.
del /q *.7z 2>nul
REM Shipping manifest - explicit and toolchain-independent. Both build routes
REM converge on exactly these root files; a missing entry FAILS the build here
REM instead of silently packing a short archive (a bare *.bat wildcard would
REM match nothing without any error - see compile_guidance gotcha 5).
REM ExCSS + the facade dlls are load-time dependencies of the main dll (SVG
REM renderer path + Span/Memory shims); omitting them makes a fresh machine hit
REM FileLoadException on the first vector-drawable icon.
REM System.Numerics.Vectors.dll IS REQUIRED app-local: inside COM hosts
REM (explorer/dllhost) TypeDescriptor builds Svg.NET's numeric converters, and
REM that binding probes the app base only - a missing file means
REM FileNotFoundException on EVERY numeric SVG attribute (gradient coords,
REM offsets...), which silently renders adaptive icons as blank/fragmented
REM tiles (observed 2026-11-21 as the root cause of the broken-icon report).
set "MANIFEST=ApkShellext2.dll install.bat install_user.bat uninstall.bat uninstall_user.bat restart_explorer.bat libwebp_x86.dll libwebp_x64.dll ExCSS.dll System.Memory.dll System.Buffers.dll System.Runtime.CompilerServices.Unsafe.dll System.Numerics.Vectors.dll"
for %%F in (%MANIFEST%) do (
  if not exist "%%F" (
    echo ERROR: postbuild - manifest file missing from output: %%F
    exit /b 1
  )
)
REM Annotated EXCLUSIONS - deliberately never packed, whatever sits in the folder:
REM  System.Numerics.Vectors.dll - dotnet-CLI copy-local artifact of the NuGet
REM      graph; the .NET Framework 4.8 runtime provides it natively and VS
REM      builds never emit the file. Packing it would make archives differ per
REM      toolchain for no runtime benefit.
REM  System.Resources.Extensions.dll - preserialized-resource support, emitted
REM      only by dotnet-CLI builds. Shell extensions ship as VS-route
REM      assemblies; dotnet-CLI output is NOT Explorer-host safe (its menu dies
REM      silently in explorer because the host cannot probe this dll - full
REM      analysis in compile_guidance §8). Never re-add it to the archive, and
REM      never copy it by hand into a VS-route deployment folder.
REM Satellites live in the per-locale subfolders of TargetDir (one
REM ApkShellext2.resources.dll each). They belong in the main archive: an
REM install extracted from ApkShellext2.7z alone must show the localized UI,
REM not silently fall back to English. 7z stores the folder path, so the
REM locale directories come out of the archive exactly where the GAC-less
REM probing expects them.
set "SATS="
for %%L in (ar-SA de-DE el-GR es-ES fa-IR fr-FR he-IL it-IT ja-JP ko-KR my-MM nl-NL pl-PL pt-PT ru-RU tr-TR uk-UA zh-CN zh-TW) do if exist "%%L\ApkShellext2.resources.dll" set "SATS=!SATS! %%L"
"%Z7%" a ApkShellext2.7z %MANIFEST% !SATS!
REM No per-language archives anymore: the main package carries every locale,
REM so the old one-7z-per-language split (upstream distribution style) would
REM only clutter the output folder.
exit /b 0
