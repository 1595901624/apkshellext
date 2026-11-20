@echo off
rem Post-build steps for ApkShellext2, invoked from ApkShellext2.csproj:
rem   call "$(ProjectDir)Resources\postbuild.cmd" "$(TargetDir)" "$(ProjectDir)" [Configuration]
rem Kept as a real .cmd file on purpose: PostBuildEvent inline batch text goes
rem through an MSBuild-generated temp .cmd where line continuations, %% escaping
rem and block parsing are fragile. A plain file avoids all of that.
setlocal
set "TDIR=%~1"
set "PDIR=%~2"
set "CONF=%~3"
if "%CONF%"=="" set "CONF=Debug"

copy /Y "%PDIR%Resources\install.bat"         "%TDIR%"
copy /Y "%PDIR%Resources\install_user.bat"    "%TDIR%"
copy /Y "%PDIR%Resources\restart_explorer.bat" "%TDIR%"
copy /Y "%PDIR%Resources\uninstall.bat"       "%TDIR%"
copy /Y "%PDIR%Resources\uninstall_user.bat"  "%TDIR%"
copy /Y "%PDIR%LibWebP\libwebp_x64.dll"       "%TDIR%"
copy /Y "%PDIR%LibWebP\libwebp_x86.dll"       "%TDIR%"

if not "%CONF%"=="Release" goto :debugbranch

rem debug.bat is a dev helper, only ever copied in Debug builds - delete a
rem stale one so the Release *.bat wildcard does not package it.
del /q "%TDIR%debug.bat" 2>nul

cd /d "%TDIR%"

rem Locate 7z.exe without hardcoding a machine path:
rem 1) %ProgramFiles%\7-Zip  2) HKLM\SOFTWARE\7-Zip (InstallPath or Path,
rem depends on 7-Zip version)  3) PATH
set "Z7=%ProgramFiles%\7-Zip\7z.exe"
if exist "%Z7%" goto :z7found
for /f "usebackq tokens=2,*" %%i in (`reg query "HKLM\SOFTWARE\7-Zip" /v InstallPath 2^>nul ^| find /i "InstallPath"`) do set "Z7=%%j\7z.exe"
if exist "%Z7%" goto :z7found
for /f "usebackq tokens=2,*" %%i in (`reg query "HKLM\SOFTWARE\7-Zip" /v Path 2^>nul ^| find /i "Path"`) do set "Z7=%%j\7z.exe"
if exist "%Z7%" goto :z7found
for /f "usebackq tokens=2,*" %%i in (`reg query "HKLM\SOFTWARE\WOW6432Node\7-Zip" /v InstallPath 2^>nul ^| find /i "InstallPath"`) do set "Z7=%%j\7z.exe"
if exist "%Z7%" goto :z7found
for /f "usebackq delims=" %%i in (`where 7z 2^>nul`) do (set "Z7=%%i" & goto :z7found)
:z7found
if not exist "%Z7%" (
  echo WARNING: 7-Zip not found - checked ProgramFiles, registry HKLM\SOFTWARE\7-Zip and PATH. Skipping 7z packaging.
  exit /b 0
)

REM Rebuild archives from scratch: "7z a" only updates/adds, so a stale
REM archive would silently ship files that no longer exist on disk.
del /q *.7z 2>nul
REM ExCSS + the facade dlls are load-time dependencies of the main dll
REM (SVG renderer path + Span/Memory shims); omitting them makes a fresh
REM machine hit FileLoadException on the first vector-drawable icon.
"%Z7%" a ApkShellext2.7z ApkShellext2.dll *.bat libwebp_*.dll ExCSS.dll System.Memory.dll System.Buffers.dll System.Runtime.CompilerServices.Unsafe.dll
for %%L in (ar-SA de-DE el-GR es-ES fa-IR fr-FR he-IL it-IT ja-JP ko-KR my-MM nl-NL pl-PL pt-PT ru-RU tr-TR uk-UA zh-CN zh-TW) do "%Z7%" a %%L.7z %%L
exit /b 0

:debugbranch
copy /Y "%PDIR%Resources\debug.bat" "%TDIR%"
exit /b 0
