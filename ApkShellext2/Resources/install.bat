@ECHO OFF
SetLocal EnableExtensions
TITLE APK Shell Extension 2 - Install
ECHO #######################################################
ECHO ##            APK Shell Extension  2                 ##
ECHO ##      https://github.com/nkh0472/apkshellext       ##
ECHO #######################################################
ECHO.

REM === check and get the UAC for administrator privilege ===
REM elevation is launched through a 64-bit cscript (Sysnative alias when the caller
REM is a 32-bit process): 32-bit ShellExecuteEx "runas" is broken on Windows 10/11,
REM see kkguo/apkshellext#116.
>nul 2>&1 net session
if '%errorlevel%' EQU '0' goto gotAdmin
if '%~1' EQU '1' (
    echo Cannot elevate administrator privilege
    echo Please try again with "Run as Administrator"
    echo Installation failed.
    pause
    exit /b 1
)

:UACPrompt
    echo Requesting administrative privileges...
    set "CSCRIPT=%SystemRoot%\System32\cscript.exe"
    if exist "%SystemRoot%\Sysnative\cscript.exe" set "CSCRIPT=%SystemRoot%\Sysnative\cscript.exe"
    REM random name: fixed %temp%\*.vbs / *.reg names in a world-writable dir
    REM can be pre-created/rewritten by another process before we run them
    set "ELEV_VBS=%temp%\apkshellext_elev_%RANDOM%.vbs"
    echo CreateObject("Shell.Application").ShellExecute "%~f0", "1", "", "runas" > "%ELEV_VBS%"
    "%CSCRIPT%" //nologo "%ELEV_VBS%"
    del "%ELEV_VBS%" 2>nul
    exit /b

:gotAdmin
    CD /D "%~dp0"
ECHO.

if not exist "%~dp0ApkShellext2.dll" (
    echo ERROR: ApkShellext2.dll not found next to this script.
    echo Extract the whole archive to the final folder first, then run install.bat again.
    pause
    exit /b 1
)

REM === refuse to register a user-writable folder machine-wide ============
REM /codebase puts this folder's DLL path into HKLM, so EVERY user's explorer
REM loads it. If the folder is writable by unprivileged users (typical when
REM the zip was extracted to Downloads/Desktop), any standard user can later
REM replace the DLL and run code as the other users, admins included.
REM Precise test: ACL grants Write/Modify/Full to Everyone / Authenticated
REM Users / Built-in Users? (named-user-only ownership is not a cross-user risk)
powershell -NoProfile -ExecutionPolicy Bypass -Command "$bad=$false; foreach($a in (Get-Acl -LiteralPath '%~dp0.').Access){ if($a.AccessControlType -ne 'Allow'){continue}; try{$sid=$a.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value}catch{continue}; if($sid -in 'S-1-1-0','S-1-5-11','S-1-5-32-545'){ if([string]$a.FileSystemRights -match 'Write|Modify|FullControl'){$bad=$true} } }; if($bad){exit 2}"
REM exit 2 = risky ACE found (exit 1 = powershell itself failed: do not block install on that)
if '%errorlevel%' EQU '2' (
    echo ERROR: this install folder grants write access to ordinary users.
    echo A machine-wide install loads this DLL into every user's explorer, so
    echo the folder must be protected against replacement.
    echo Move the extracted folder to a protected location, e.g.
    echo     "%ProgramFiles%\ApkShellext2"
    echo or install per-user without admin rights using install_user.bat instead.
    pause
    exit /b 1
)

set "REGASM64=%windir%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
set "REGASM32=%windir%\Microsoft.NET\Framework\v4.0.30319\regasm.exe"

if not exist "%REGASM64%" (
    echo ERROR: .NET Framework 4.x regasm.exe not found at "%REGASM64%".
    pause
    exit /b 1
)

REM === register with BOTH regasm bitnesses ===
REM 64-bit view serves explorer.exe; 32-bit view serves 32-bit host processes
REM (older file dialogs etc). Registering only one view leaves the other hosts
REM without icons and is a known source of half-uninstalled states.
ECHO [1/5] Registering shell handlers (64-bit view)...
"%REGASM64%" /codebase "%~dp0ApkShellext2.dll"
if not '%errorlevel%' EQU '0' (
    echo ERROR: 64-bit registration failed with code %errorlevel%.
    pause
    exit /b 1
)
if exist "%REGASM32%" (
    ECHO [1/5] Registering shell handlers (32-bit view)...
    "%REGASM32%" /codebase "%~dp0ApkShellext2.dll"
    if not '%errorlevel%' EQU '0' echo WARNING: 32-bit registration failed; 64-bit hosts still work.
)

REM === NGEN precompile: native images remove the JIT cost from every
REM Explorer start (shell extensions load in-process, first-icon latency is
REM the user-visible effect). Failures are non-fatal: machines with the
REM Trusted Platform module service disabled cannot queue images. ===
set "NGEN64=%windir%\Microsoft.NET\Framework64\v4.0.30319\ngen.exe"
set "NGEN32=%windir%\Microsoft.NET\Framework\v4.0.30319\ngen.exe"
ECHO.
ECHO [2/5] Generating native images (NGEN)...
"%NGEN64%" install "%~dp0ApkShellext2.dll" /quiet
if not '%errorlevel%' EQU '0' echo WARNING: 64-bit NGEN failed (service disabled?); icons still work.
if exist "%NGEN32%" (
    "%NGEN32%" install "%~dp0ApkShellext2.dll" /quiet
    if not '%errorlevel%' EQU '0' echo WARNING: 32-bit NGEN failed; 64-bit hosts still work.
)

REM === show which DLL the shell will actually load (both views) ===
ECHO.
ECHO [3/5] Active registration (both views):
ECHO     this folder: %~dp0
for %%V in (
    "HKLM\SOFTWARE\Classes\CLSID\{1F869CEE-4FDA-35D9-896F-43975A87D1F6}\InprocServer32"
    "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\{1F869CEE-4FDA-35D9-896F-43975A87D1F6}\InprocServer32"
) do (
    reg query %%V /v CodeBase 2>nul | findstr /i CodeBase
    if errorlevel 1 echo     (view has no CodeBase entry: %%~V)
)
echo     If a CodeBase above points to ANOTHER folder, an old copy is still
echo     registered - run uninstall.bat from that old folder as well.

REM === notify shell + restart explorer so new handlers load immediately ===
ECHO.
ECHO [4/5] Notifying the shell...
ie4uinit.exe -show >nul 2>&1
choice /C YN /M "Restart Explorer now to take effect (recommended)"
if '%errorlevel%' EQU '2' goto verify
taskkill /f /im explorer.exe >nul 2>&1
timeout /t 2 /nobreak >nul
start "" explorer.exe

:verify

REM === verify ===
ECHO.
ECHO [5/5] Verifying...
reg query "HKLM\SOFTWARE\Classes\CLSID\{1F869CEE-4FDA-35D9-896F-43975A87D1F6}" >nul 2>&1
if '%errorlevel%' EQU '0' (
    echo Install OK: icon handler registered and pointing at "%~dp0".
) else (
    echo WARNING: verification failed - CLSID not found in 64-bit view after install.
)
ECHO.
PAUSE
exit /b 0
