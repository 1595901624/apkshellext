@ECHO OFF
SetLocal EnableExtensions EnableDelayedExpansion
TITLE APK Shell Extension 2 - Per-user Uninstall
ECHO #######################################################
ECHO ##    APK Shell Extension  2  -  per-user removal    ##
ECHO #######################################################
ECHO.

REM === removes what install_user.bat registered (HKCU\Software\Classes only) ===
REM No administrator privilege needed. Does not touch HKLM machine-wide
REM registrations - use uninstall.bat for those.

CD /D "%~dp0"

ECHO [1/3] Sweeping per-user registration...
for %%G in (
    {1F869CEE-4FDA-35D9-896F-43975A87D1F6}
    {D747C5A7-2F66-4B7D-8301-8531838E4ED3}
    {DCB629FC-F86F-456F-8E24-98B9B2643A9B}
    {946435A5-FE96-416D-99DB-E94EE9FB46C8}
) do (
    reg delete "HKCU\SOFTWARE\Classes\CLSID\%%G" /f >nul 2>&1
    reg delete "HKCU\SOFTWARE\Classes\Wow6432Node\CLSID\%%G" /f >nul 2>&1
)
reg delete "HKCU\SOFTWARE\Classes\ApkShellext2.ApkFile" /f >nul 2>&1
for %%P in (ApkContextMenu ApkIconHandler ApkInfoTipHandler ApkThumbnailHandler) do reg delete "HKCU\SOFTWARE\Classes\ApkShellext2.%%P" /f >nul 2>&1
REM Association cleanup in the layout SharpShell actually writes: handler slots
REM live under the extension's CLASS key (default value of the extension), not
REM under the extension or SystemFileAssociations. Remove only slots whose
REM value is one of our GUIDs, then undo the DefaultIcon hijack.
SetLocal EnableExtensions EnableDelayedExpansion
for %%E in (.apk .ipa .aab .appx .appxbundle .apks .apkm .xapk) do (
    set "EXT=%%~xE"
    set "CN="
    for /f "tokens=2,*" %%A in ('reg query "HKCU\SOFTWARE\Classes\%%E" /ve 2^>nul ^| findstr REG_SZ') do set "CN=%%B"
    if not defined CN (
        REM %%~xE of '.apk' keeps the dot - strip it so the class is 'apk.1'
        set "CN=!EXT:~1!.1"
    )
    call :dropours "HKCU\SOFTWARE\Classes\!CN!\ShellEx\IconHandler"                                   {1F869CEE-4FDA-35D9-896F-43975A87D1F6}
    call :dropours "HKCU\SOFTWARE\Classes\!CN!\ShellEx\{00021500-0000-0000-C000-000000000046}"          {946435A5-FE96-416D-99DB-E94EE9FB46C8}
    call :dropours "HKCU\SOFTWARE\Classes\!CN!\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}"          {D747C5A7-2F66-4B7D-8301-8531838E4ED3}
    REM context menu slot is named after the handler class - match by GUID value instead
    for /f "delims=" %%S in ('reg query "HKCU\SOFTWARE\Classes\!CN!\ShellEx\ContextMenuHandlers" 2^>nul') do call :dropours "%%S" {DCB629FC-F86F-456F-8E24-98B9B2643A9B}
    REM restore DefaultIcon: our backup wins, else a bare %%1 default is ours - drop it
    set "BAK="
    for /f "tokens=2,*" %%A in ('reg query "HKCU\SOFTWARE\Classes\!CN!\DefaultIcon" /v SharpShell_Backup_DefaultIcon 2^>nul ^| findstr SharpShell_Backup_DefaultIcon') do set "BAK=%%B"
    if defined BAK (
        reg add "HKCU\SOFTWARE\Classes\!CN!\DefaultIcon" /ve /d "!BAK!" /f >nul 2>&1
        reg delete "HKCU\SOFTWARE\Classes\!CN!\DefaultIcon" /v SharpShell_Backup_DefaultIcon /f >nul 2>&1
    ) else (
        for /f "tokens=2,*" %%A in ('reg query "HKCU\SOFTWARE\Classes\!CN!\DefaultIcon" /ve 2^>nul ^| findstr REG_SZ') do if "%%B"=="%%1" reg delete "HKCU\SOFTWARE\Classes\!CN!\DefaultIcon" /f >nul 2>&1
    )
    call :dropempty "HKCU\SOFTWARE\Classes\!CN!\ShellEx\ContextMenuHandlers"
    call :dropempty "HKCU\SOFTWARE\Classes\!CN!\ShellEx"
    call :cleancls "HKCU\SOFTWARE\Classes" "!CN!" "!EXT:~1!" %%E
)
EndLocal
goto :sweepzoo

:cleancls
REM %1=classes root %2=class name %3=extension without dot %4=extension with dot.
REM Removes the '<name>.1' class + extension pointer ONLY when every trace shows
REM the installer created them (own default text, no live ShellEx children, no
REM DefaultIcon). Never touches classes another application owns.
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

:dropours
REM %1 = key, %2 = expected default-value GUID; deletes the key only when it is ours
set "V="
for /f "tokens=2,*" %%A in ('reg query "%~1" /ve 2^>nul ^| findstr REG_SZ') do set "V=%%B"
if /i "!V!"=="%~2" reg delete "%~1" /f >nul 2>&1
goto :eof

:dropempty
REM deletes %1 only when it has no subkeys (never disturbs other vendors' slots)
set "ANY="
for /f "delims=" %%S in ('reg query "%~1" 2^>nul ^| findstr /i "HKEY_"') do if /i not "%%S"=="%~1" set "ANY=1"
if not defined ANY reg delete "%~1" /f >nul 2>&1
goto :eof

:sweepzoo

REM Any remaining per-user CLSID whose data references our DLL (covers legacy
REM dependency COM entries created by a pre-3.4.2 install_user.bat).
for /f "delims=" %%K in ('reg query "HKCU\SOFTWARE\Classes\CLSID" /s /f "ApkShellext2.dll" /d /k 2^>nul') do (
    reg delete "%%K" /f >nul 2>&1
    set "KP=%%K"
    set "KP=!KP:\InprocServer32=!"
    reg delete "!KP!" /f >nul 2>&1
)
REM Per-user HKCR\Record mirror entries owned by this assembly.
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Test-Path 'HKCU:\Software\Classes\Record') { Get-ChildItem 'HKCU:\Software\Classes\Record' | ForEach-Object { $g=$_; if (Get-ChildItem $g.PSPath | Get-ItemProperty -Name Assembly -ErrorAction SilentlyContinue | Where-Object { $_.Assembly -like 'ApkShellext2*' }) { Remove-Item $g.PSPath -Recurse -Force } } }" >nul 2>&1

ECHO.
ECHO [2/3] Verifying...
set "LEFTOVER="
for %%G in (
    {1F869CEE-4FDA-35D9-896F-43975A87D1F6}
    {D747C5A7-2F66-4B7D-8301-8531838E4ED3}
    {DCB629FC-F86F-456F-8E24-98B9B2643A9B}
    {946435A5-FE96-416D-99DB-E94EE9FB46C8}
) do reg query "HKCU\SOFTWARE\Classes\CLSID\%%G" >nul 2>&1 && set "LEFTOVER=1"
if defined LEFTOVER (
    echo WARNING: some per-user CLSID entries are still present.
) else (
    echo All per-user ApkShellext2 registrations removed.
)

ECHO.
choice /C YN /M "[3/3] Restart Explorer now (recommended)"
if '%errorlevel%' EQU '2' goto done
taskkill /f /im explorer.exe >nul 2>&1
timeout /t 2 /nobreak >nul
start "" explorer.exe

:done
ECHO.
PAUSE
exit /b 0
