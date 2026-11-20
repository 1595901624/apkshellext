@ECHO OFF
SetLocal EnableExtensions
TITLE APK Shell Extension 2 - Per-user Install
ECHO #######################################################
ECHO ##      APK Shell Extension  2  -  per-user mode     ##
ECHO ##      HKCU registration, no administrator needed   ##
ECHO #######################################################
ECHO.

REM === per-user (HKCU\Software\Classes) registration ===
REM Modern alternative to the machine-wide install.bat: no UAC prompt, applies
REM to the current user only, and is removed again by uninstall_user.bat.
REM Explorer loads shell handlers from the per-user registry view the same way
REM as from HKLM. NGEN is intentionally skipped (it requires admin; the JIT
REM cost only shows on the very first icons after boot).

CD /D "%~dp0"

if not exist "%~dp0ApkShellext2.dll" (
    echo ERROR: ApkShellext2.dll not found next to this script.
    echo Extract the whole archive to the final folder first, then run again.
    pause
    exit /b 1
)

set "REGASM64=%windir%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
if not exist "%REGASM64%" (
    echo ERROR: .NET Framework 4.x regasm.exe not found at "%REGASM64%".
    pause
    exit /b 1
)

ECHO [1/5] Generating registration file...
REM random names: fixed %TEMP% file names can be pre-created by another
REM same-user process before we write/import them
set "REGF=%TEMP%\apksext_user_%RANDOM%.reg"
set "REGFH=%TEMP%\apksext_user_hkcu_%RANDOM%.reg"
"%REGASM64%" /codebase /regfile:"%REGF%" "%~dp0ApkShellext2.dll"
if not '%errorlevel%' EQU '0' (
    echo ERROR: regasm /regfile failed with code %errorlevel%.
    pause
    exit /b 1
)

ECHO [2/5] Redirecting HKCR to HKCU\Software\Classes and importing...
powershell -NoProfile -ExecutionPolicy Bypass -Command "(Get-Content -LiteralPath '%REGF%' -Raw) -replace 'HKEY_CLASSES_ROOT','HKEY_CURRENT_USER\Software\Classes' | Set-Content -LiteralPath '%REGFH%' -Encoding Unicode"
if not exist "%REGFH%" (
    echo ERROR: PowerShell retarget step failed ^(is powershell.exe available?^).
    pause
    exit /b 1
)
reg import "%REGFH%"
if not '%errorlevel%' EQU '0' (
    echo ERROR: reg import failed with code %errorlevel%.
    pause
    exit /b 1
)
del "%REGF%" "%REGFH%" >nul 2>&1

REM regasm /regfile cannot carry the file-type associations (SharpShell writes
REM them during its installer phase, which /regfile skips), and HKCU imports
REM never run installers. Write them here in exactly the same layout as the
REM machine-wide install: handlers live under the extension's CLASS key
REM (HKCU\Software\Classes\<class>, class = default value of the extension,
REM created as '<name>.1' when the extension has no class yet).
ECHO [3/5] Writing file-type associations (per-user)...
SetLocal EnableExtensions EnableDelayedExpansion
for %%E in (.apk .ipa .aab .appx .appxbundle .apks .apkm .xapk) do (
    set "CN="
    for /f "tokens=2,*" %%A in ('reg query "HKCU\SOFTWARE\Classes\%%E" /ve 2^>nul ^| findstr REG_SZ') do set "CN=%%B"
    if not defined CN (
        REM %%~xE of '.apk' keeps the dot - strip it so the class is 'apk.1'
        set "EXT=%%~xE"
        set "CN=!EXT:~1!.1"
        reg add "HKCU\SOFTWARE\Classes\!CN!" /ve /d "!EXT:~1! Application" /f >nul 2>&1
        reg add "HKCU\SOFTWARE\Classes\%%E" /ve /d "!CN!" /f >nul 2>&1
    )
    REM icon handler: all extensions
    reg add "HKCU\SOFTWARE\Classes\!CN!\ShellEx\IconHandler" /ve /d "{1F869CEE-4FDA-35D9-896F-43975A87D1F6}" /f >nul 2>&1
    REM info-tip + context menu: everything except .aab (mirrors handler attributes)
    if /I not "%%E"==".aab" (
        reg add "HKCU\SOFTWARE\Classes\!CN!\ShellEx\{00021500-0000-0000-C000-000000000046}" /ve /d "{946435A5-FE96-416D-99DB-E94EE9FB46C8}" /f >nul 2>&1
        reg add "HKCU\SOFTWARE\Classes\!CN!\ShellEx\ContextMenuHandlers\ApkContextMenu" /ve /d "{DCB629FC-F86F-456F-8E24-98B9B2643A9B}" /f >nul 2>&1
    )
    REM thumbnail handler: .apk only (mirrors handler attribute)
    if /I "%%E"==".apk" (
        reg add "HKCU\SOFTWARE\Classes\!CN!\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}" /ve /d "{D747C5A7-2F66-4B7D-8301-8531838E4ED3}" /f >nul 2>&1
    )
    REM icon handler hijacks the class DefaultIcon; back up whatever was there
    REM (SharpShell does the same and names the backup value as below)
    set "OLD="
    for /f "tokens=2,*" %%A in ('reg query "HKCU\SOFTWARE\Classes\!CN!\DefaultIcon" /ve 2^>nul ^| findstr REG_SZ') do set "OLD=%%B"
    if defined OLD reg add "HKCU\SOFTWARE\Classes\!CN!\DefaultIcon" /v SharpShell_Backup_DefaultIcon /d "!OLD!" /f >nul 2>&1
    reg add "HKCU\SOFTWARE\Classes\!CN!\DefaultIcon" /ve /d "%%1" /f >nul 2>&1
)
EndLocal

ECHO.
ECHO [4/5] Notifying the shell...
ie4uinit.exe -show >nul 2>&1
choice /C YN /M "Restart Explorer now to take effect (recommended)"
if '%errorlevel%' EQU '2' goto verify
taskkill /f /im explorer.exe >nul 2>&1
timeout /t 2 /nobreak >nul
start "" explorer.exe

:verify
ECHO.
ECHO [5/5] Verifying...
reg query "HKCU\SOFTWARE\Classes\CLSID\{1F869CEE-4FDA-35D9-896F-43975A87D1F6}" >nul 2>&1
if '%errorlevel%' EQU '0' (
    echo Per-user install OK: handlers registered under HKCU for this account.
    echo NOTE: if a machine-wide install exists in HKLM, that registration wins.
) else (
    echo WARNING: verification failed - CLSID not found under HKCU after import.
)
ECHO.
PAUSE
exit /b 0
