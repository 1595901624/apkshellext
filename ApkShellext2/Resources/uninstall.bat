@ECHO OFF
SetLocal EnableExtensions EnableDelayedExpansion
TITLE APK Shell Extension 2 - Uninstall
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
    echo Uninstallation failed.
    pause
    exit /b 1
)

:UACPrompt
    echo Requesting administrative privileges...
    set "CSCRIPT=%SystemRoot%\System32\cscript.exe"
    if exist "%SystemRoot%\Sysnative\cscript.exe" set "CSCRIPT=%SystemRoot%\Sysnative\cscript.exe"
    echo CreateObject("Shell.Application").ShellExecute "%~f0", "1", "", "runas" > "%temp%\apkshellext_elev.vbs"
    "%CSCRIPT%" //nologo "%temp%\apkshellext_elev.vbs"
    del "%temp%\apkshellext_elev.vbs" 2>nul
    exit /b

:gotAdmin
    CD /D "%~dp0"
ECHO.

set "REGASM64=%windir%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
set "REGASM32=%windir%\Microsoft.NET\Framework\v4.0.30319\regasm.exe"

REM === step 1: ordinary regasm /unregister when the DLL is still here ===
if not exist "%~dp0ApkShellext2.dll" (
    echo NOTE: ApkShellext2.dll is not next to this script.
    echo       Skipping regasm /unregister; performing full registry sweep instead.
    echo       ^(This also cleans copies registered from OTHER folders - same CLSIDs.^)
    goto sweep
)
ECHO [1/5] Unregistering shell handlers (64-bit view)...
"%REGASM64%" /unregister "%~dp0ApkShellext2.dll"
if exist "%REGASM32%" (
    ECHO [1/5] Unregistering shell handlers (32-bit view)...
    "%REGASM32%" /unregister "%~dp0ApkShellext2.dll"
)

:sweep
REM === step 2: full registry sweep in BOTH views ===
REM regasm /unregister only cleans entries matching the DLL it is given and only in
REM its own bitness view; registrations left by other install folders or the other
REM regasm view survive it. Wipe the known CLSIDs, ProgID and extension hooks here.
ECHO.
ECHO [2/5] Sweeping registry (64-bit + 32-bit views)...
for %%G in (
    {1F869CEE-4FDA-35D9-896F-43975A87D1F6}
    {D747C5A7-2F66-4B7D-8301-8531838E4ED3}
    {DCB629FC-F86F-456F-8E24-98B9B2643A9B}
    {946435A5-FE96-416D-99DB-E94EE9FB46C8}
) do (
    reg delete "HKLM\SOFTWARE\Classes\CLSID\%%G" /f >nul 2>&1
    reg delete "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\%%G" /f >nul 2>&1
    reg delete "HKLM\SOFTWARE\Classes\WOW6432Node\CLSID\%%G" /f >nul 2>&1
)
for %%V in (
    "HKLM\SOFTWARE\Classes"
    "HKLM\SOFTWARE\WOW6432Node\Classes"
) do (
    reg delete %%V\ApkShellext2.ApkFile /f >nul 2>&1
    for %%E in (.apk .ipa .aab .appx .appxbundle .apks .apkm .xapk) do (
        for %%G in (
            {1F869CEE-4FDA-35D9-896F-43975A87D1F6}
            {D747C5A7-2F66-4B7D-8301-8531838E4ED3}
            {DCB629FC-F86F-456F-8E24-98B9B2643A9B}
            {946435A5-FE96-416D-99DB-E94EE9FB46C8}
        ) do reg delete %%V\SystemFileAssociations\%%E\ShellEx\%%G /f >nul 2>&1
    )
)

:legacy
REM === step 3: legacy COM entry cleanup (builds up to 3.4.1 used assembly-level
REM [ComVisible(true)], so regasm registered EVERY public type of the source-embedded
REM dependencies (Svg.*, SharpShell.*, ICSharpCode.*, ...) plus HKCR\Record entries.
REM The current regasm /unregister no longer sees those - remove them by owner. ===
ECHO.
ECHO [3/5] Cleaning legacy COM entries from pre-3.4.2 builds...
for %%V in (
    "HKLM\SOFTWARE\Classes\CLSID"
    "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID"
) do (
    for /f "delims=" %%K in ('reg query %%V /s /f "ApkShellext2.dll" /d /k 2^>nul') do (
        reg delete "%%K" /f >nul 2>&1
        set "KP=%%K"
        set "KP=!KP:\InprocServer32=!"
        reg delete "!KP!" /f >nul 2>&1
    )
)
REM ProgIDs left by those registrations: delete "<vendored namespace>.<type>" keys
REM only when the CLSID they point to no longer resolves (never touches other
REM vendors' live registrations).
for %%P in (Svg. SharpShell. ICSharpCode. WebPWrapper. QRCoder PlistCS PNGDecrush. ApkShellext2.) do (
    for %%V in (
        "HKLM\SOFTWARE\Classes"
        "HKLM\SOFTWARE\WOW6432Node\Classes"
    ) do (
        for /f "delims=" %%K in ('reg query %%V /f "%%P" /k 2^>nul ^| findstr /i /c:"\%%P"') do (
            set "TGT="
            for /f "tokens=3" %%X in ('reg query "%%K\CLSID" 2^>nul ^| findstr /i "REG_SZ"') do set "TGT=%%X"
            if defined TGT (
                reg query "%%V\CLSID\!TGT!" >nul 2>&1 || reg delete "%%K" /f >nul 2>&1
            )
        )
    )
)
REM HKCR\Record entries (per-type marshalling records) owned by this assembly.
REM Layout: Record\{guid}\<version> holds value Assembly=ApkShellext2, ...
for %%V in (
    "HKLM\SOFTWARE\Classes\Record"
    "HKLM\SOFTWARE\WOW6432Node\Classes\Record"
) do (
    for /f "delims=" %%A in ('reg query %%V 2^>nul ^| findstr /i "{"') do (
        for /f "delims=" %%B in ('reg query "%%A" 2^>nul ^| findstr /i "HKEY"') do (
            reg query "%%B" /v Assembly 2>nul | findstr /i /c:"ApkShellext2" >nul 2>&1 && reg delete "%%A" /f >nul 2>&1
        )
    )
)

REM === step 4: verify ===
ECHO.
ECHO [4/5] Verifying...
set "LEFTOVER="
for %%G in (
    {1F869CEE-4FDA-35D9-896F-43975A87D1F6}
    {D747C5A7-2F66-4B7D-8301-8531838E4ED3}
    {DCB629FC-F86F-456f-8e24-98b9b2643a9b}
    {946435A5-FE96-416D-99DB-E94EE9FB46C8}
) do (
    reg query "HKLM\SOFTWARE\Classes\CLSID\%%G" >nul 2>&1 && set "LEFTOVER=1"
    reg query "HKLM\SOFTWARE\WOW6432Node\Classes\CLSID\%%G" >nul 2>&1 && set "LEFTOVER=1"
)
if defined LEFTOVER (
    echo WARNING: some CLSID entries are still present - check permissions.
) else (
    echo All ApkShellext2 registrations removed.
)

REM === step 4: optional explorer restart (frees the DLL file lock and drops
REM in-memory handler objects; icon cache is intentionally left alone) ===
ECHO.
choice /C YN /M "[5/5] Restart Explorer now (recommended before deleting the folder)"
if '%errorlevel%' EQU '2' goto done
taskkill /f /im explorer.exe >nul 2>&1
timeout /t 2 /nobreak >nul
start "" explorer.exe

:done
ECHO.
ECHO Uninstall finished. You can now delete the program folder
ECHO (best done after Explorer has restarted, the DLL may be locked until then).
ECHO.
PAUSE
exit /b 0
