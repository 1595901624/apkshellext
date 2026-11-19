@ECHO OFF
ECHO #######################################################
ECHO ##            APK Shell Extension  2                 ##
ECHO ##                                                   ##
ECHO ##           http://apkshellext.com      s            ##
ECHO #######################################################

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
    pushd "%CD%"
    CD /D "%~dp0"
:--------------------------------------

REM pick the 64-bit regasm on 64-bit Windows (PROCESSOR_ARCHITEW6432 set when a
REM 32-bit caller was somehow left elevated; the vbs re-launch above avoids it)
set FRAMEWORK=%windir%\Microsoft.NET\Framework
if /i "%PROCESSOR_ARCHITECTURE%"=="AMD64" set FRAMEWORK=%FRAMEWORK%64
if /i "%PROCESSOR_ARCHITEW6432%"=="AMD64" set FRAMEWORK=%windir%\Microsoft.NET\Framework64
set DOTNETVERSION=v4.0.30319
set REGASM="%FRAMEWORK%\%DOTNETVERSION%\regasm.exe"
%REGASM% /unregister "%~dp0\apkshellext2.dll"

ECHO.
REM ECHO The apkshellext.dll may can't be deleted before explorer restart.

ECHO.
ECHO /-------------------------------------------------------------------\
ECHO  apkshellext is an open-source project,
ECHO  Please visit http://apkshellext.com for more information
ECHO \-------------------------------------------------------------------/

PAUSE
@ECHO ON
