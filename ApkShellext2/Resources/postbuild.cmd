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
REM ExCSS + the facade dlls are load-time dependencies of the main dll
REM (SVG renderer path + Span/Memory shims); omitting them makes a fresh
REM machine hit FileLoadException on the first vector-drawable icon.
REM System.Resources.Extensions.dll is present only for dotnet-CLI builds: the
REM SDK-style project enables GenerateResourceUsePreserializedResources under
REM coreclr MSBuild, and preserialized .resources need that assembly at RUNTIME
REM (verified: without it, reading NonLocalizeResources throws FileNotFoundException
REM for System.Resources.Extensions v4.0.0.0). A Visual Studio (FullFramework)
REM build uses classic resources and has no such file - package it if present.
set "SRE="
if exist "System.Resources.Extensions.dll" set "SRE=System.Resources.Extensions.dll"
REM Satellites live in the per-locale subfolders of TargetDir (one
REM ApkShellext2.resources.dll each). They belong in the main archive: an
REM install extracted from ApkShellext2.7z alone must show the localized UI,
REM not silently fall back to English. 7z stores the folder path, so the
REM locale directories come out of the archive exactly where the GAC-less
REM probing expects them.
set "SATS="
for %%L in (ar-SA de-DE el-GR es-ES fa-IR fr-FR he-IL it-IT ja-JP ko-KR my-MM nl-NL pl-PL pt-PT ru-RU tr-TR uk-UA zh-CN zh-TW) do if exist "%%L\ApkShellext2.resources.dll" set "SATS=!SATS! %%L"
"%Z7%" a ApkShellext2.7z ApkShellext2.dll *.bat libwebp_*.dll ExCSS.dll System.Memory.dll System.Buffers.dll System.Runtime.CompilerServices.Unsafe.dll %SRE% !SATS!
REM No per-language archives anymore: the main package carries every locale,
REM so the old one-7z-per-language split (upstream distribution style) would
REM only clutter the output folder.
exit /b 0
