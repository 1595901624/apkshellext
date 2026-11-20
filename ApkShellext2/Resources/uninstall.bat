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
set "NGEN64=%windir%\Microsoft.NET\Framework64\v4.0.30319\ngen.exe"
set "NGEN32=%windir%\Microsoft.NET\Framework\v4.0.30319\ngen.exe"
if not exist "%~dp0ApkShellext2.dll" (
    echo NOTE: ApkShellext2.dll is not next to this script.
    echo       Skipping regasm /unregister; performing full registry sweep instead.
    echo       ^(This also cleans copies registered from OTHER folders - same CLSIDs.^)
    REM NGEN images can still be dropped by assembly identity without the file.
    REM Versions before 3.4.4 used assembly version 0.3.0.0; newer ones match the
REM release number. Try both identities so images from any past install die.
"%NGEN64%" uninstall "ApkShellext2, Version=0.3.0.0, Culture=neutral, PublicKeyToken=fca4c8ef6b263859" /nologo >nul 2>&1
"%NGEN64%" uninstall "ApkShellext2, Version=3.4.4.0, Culture=neutral, PublicKeyToken=fca4c8ef6b263859" /nologo >nul 2>&1
"%NGEN64%" uninstall "ApkShellext2, Version=3.4.5.0, Culture=neutral, PublicKeyToken=fca4c8ef6b263859" /nologo >nul 2>&1
    if exist "%NGEN32%" "%NGEN32%" uninstall "ApkShellext2, Version=0.3.0.0, Culture=neutral, PublicKeyToken=fca4c8ef6b263859" /nologo >nul 2>&1
if exist "%NGEN32%" "%NGEN32%" uninstall "ApkShellext2, Version=3.4.4.0, Culture=neutral, PublicKeyToken=fca4c8ef6b263859" /nologo >nul 2>&1
if exist "%NGEN32%" "%NGEN32%" uninstall "ApkShellext2, Version=3.4.5.0, Culture=neutral, PublicKeyToken=fca4c8ef6b263859" /nologo >nul 2>&1
    goto sweep
)
ECHO [1/6] Unregistering shell handlers (64-bit view)...
"%REGASM64%" /unregister "%~dp0ApkShellext2.dll"
if exist "%REGASM32%" (
    ECHO [1/6] Unregistering shell handlers ^(32-bit view^)...
    "%REGASM32%" /unregister "%~dp0ApkShellext2.dll"
)

REM === step 2: drop the NGEN native images (installed by install.bat step 2) ===
ECHO.
ECHO [2/6] Removing native images (NGEN)...
"%NGEN64%" uninstall "%~dp0ApkShellext2.dll" /nologo
if exist "%NGEN32%" "%NGEN32%" uninstall "%~dp0ApkShellext2.dll" /nologo

:sweep
REM === step 3: full registry sweep in BOTH views ===
REM regasm /unregister only cleans entries matching the DLL it is given and only in
REM its own bitness view; registrations left by other install folders or the other
REM regasm view survive it. Wipe the known CLSIDs, ProgID and extension hooks here.
ECHO.
ECHO [3/6] Sweeping registry (64-bit + 32-bit views)...
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
        REM handler slots live under the extension CLASS key - the default value
        REM of the extension entry. SystemFileAssociations entries also removed.
        set "EXT=%%~xE"
        set "CN="
        for /f "tokens=2,*" %%A in ('reg query %%V\%%E /ve 2^>nul ^| findstr REG_SZ') do set "CN=%%B"
        if not defined CN (
            REM %%~xE of '.apk' keeps the dot - strip it so the class is 'apk.1'
            set "CN=!EXT:~1!.1"
        )
        call :dropours "%%~V\!CN!\ShellEx\IconHandler"                          {1F869CEE-4FDA-35D9-896F-43975A87D1F6}
        call :dropours "%%~V\!CN!\ShellEx\{00021500-0000-0000-C000-000000000046}" {946435A5-FE96-416D-99DB-E94EE9FB46C8}
        call :dropours "%%~V\!CN!\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}" {D747C5A7-2F66-4B7D-8301-8531838E4ED3}
        for /f "delims=" %%S in ('reg query "%%~V\!CN!\ShellEx\ContextMenuHandlers" 2^>nul') do call :dropours "%%S" {DCB629FC-F86F-456F-8E24-98B9B2643A9B}
        for %%G in (
            {1F869CEE-4FDA-35D9-896F-43975A87D1F6}
            {D747C5A7-2F66-4B7D-8301-8531838E4ED3}
            {DCB629FC-F86F-456F-8E24-98B9B2643A9B}
            {946435A5-FE96-416D-99DB-E94EE9FB46C8}
        ) do reg delete %%V\SystemFileAssociations\%%E\ShellEx\%%G /f >nul 2>&1
        REM undo the icon handler DefaultIcon hijack: restore our backup, or drop a
        REM bare %%1 default that we created; anything else belongs to another app.
        set "BAK="
        for /f "tokens=2,*" %%A in ('reg query "%%~V\!CN!\DefaultIcon" /v SharpShell_Backup_DefaultIcon 2^>nul ^| findstr SharpShell_Backup_DefaultIcon') do set "BAK=%%B"
        if defined BAK (
            reg add "%%~V\!CN!\DefaultIcon" /ve /d "!BAK!" /f >nul 2>&1
            reg delete "%%~V\!CN!\DefaultIcon" /v SharpShell_Backup_DefaultIcon /f >nul 2>&1
        ) else (
            for /f "tokens=2,*" %%A in ('reg query "%%~V\!CN!\DefaultIcon" /ve 2^>nul ^| findstr REG_SZ') do if "%%B"=="%%1" reg delete "%%~V\!CN!\DefaultIcon" /f >nul 2>&1
        )
        call :dropempty "%%~V\!CN!\ShellEx\ContextMenuHandlers"
        call :dropempty "%%~V\!CN!\ShellEx"
        call :cleancls "%%~V" "!CN!" "!EXT:~1!" %%E
    )
)

:legacy
REM === step 4: legacy COM entry cleanup (builds up to 3.4.1 used assembly-level
REM [ComVisible(true)], so regasm registered EVERY public type of the source-embedded
REM dependencies (Svg.*, SharpShell.*, ICSharpCode.*, ...) plus HKCR\Record entries.
REM The current regasm /unregister no longer sees those - remove them by owner. ===
ECHO.
ECHO [4/6] Cleaning legacy COM entries from pre-3.4.2 builds...
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

REM === step 5: verify ===
ECHO.
ECHO [5/6] Verifying...
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

REM === step 6: optional explorer restart (frees the DLL file lock and drops
REM in-memory handler objects; icon cache is intentionally left alone) ===
ECHO.
choice /C YN /M "[6/6] Restart Explorer now (recommended before deleting the folder)"
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

REM ---- subroutine: delete %1 only if its default value equals the GUID %~2 ----
:dropours
set "V="
for /f "tokens=2,*" %%A in ('reg query "%~1" /ve 2^>nul ^| findstr REG_SZ') do set "V=%%B"
if /i "!V!"=="%~2" reg delete "%~1" /f >nul 2>&1
goto :eof

REM ---- subroutine: delete %1 only when it has no subkeys ----
:dropempty
set "ANY="
for /f "delims=" %%S in ('reg query "%~1" 2^>nul ^| findstr /i "HKEY_"') do if /i not "%%S"=="%~1" set "ANY=1"
if not defined ANY reg delete "%~1" /f >nul 2>&1
goto :eof

REM ---- subroutine: remove the '<name>.1' class + extension pointer only when
REM      every trace shows the installer created them (own default text, no live
REM      ShellEx children, no DefaultIcon). Never touches foreign classes. ----
:cleancls
set "NCD="
for /f "delims=" %%S in ('reg query "%~1\%~2\ShellEx" /s 2^>nul ^| findstr /i "HKEY_"') do if /i not "%%S"=="%~1\%~2\ShellEx" set "NCD=1"
if defined NCD goto :eof
set "DV="
for /f "tokens=2,*" %%A in ('reg query "%~1\%~2" /ve 2^>nul ^| findstr REG_SZ') do set "DV=%%B"
if /i not "!DV!"=="%~3 Application" goto :eof
reg query "%~1\%~2\DefaultIcon" >nul 2>&1 && goto :eof
reg delete "%~1\%~2" /f >nul 2>&1
set "DV2="
for /f "tokens=2,*" %%A in ('reg query "%~1\%~4" /ve 2^>nul ^| findstr REG_SZ') do set "DV2=%%B"
if /i "!DV2!"=="%~2" reg delete "%~1\%~4" /ve /f >nul 2>&1
goto :eof
