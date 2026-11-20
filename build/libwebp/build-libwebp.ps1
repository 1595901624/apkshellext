# Rebuild libwebp 1.6.0 native DLLs (see README.md in this folder for rationale).
# Run from this directory. Modes are mutually exclusive.
param(
    [switch]$Fetch,
    [switch]$Gcc64,
    [switch]$Msvc32,
    [switch]$Msvc64,
    [string]$Smoke,           # build dir to smoke-test (uses gcc from $GccRoot)
    [switch]$Deploy,
    [string]$SrcDir  = "$PSScriptRoot\libwebp-1.6.0-src",
    [string]$GccRoot = 'D:\msys64\ucrt64',
    [string]$Mirror  = 'https://ghfast.top/https://github.com'
)
$ErrorActionPreference = 'Stop'
$cmake = "$GccRoot\bin\cmake.exe"
$ninja = "$GccRoot\bin\ninja.exe"
$repo  = Split-Path (Split-Path $PSScriptRoot -Parent)   # repo root (X:\apkshellext)
$deployTarget = Join-Path $repo 'ApkShellext2\LibWebP'

function Invoke-Vs([string]$arch, [string]$cmd) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $vs = & $vswhere -latest -prerelease -property installationPath
    if (-not $vs) { throw "VS not found via vswhere" }
    $devcmd = Join-Path $vs 'Common7\Tools\VsDevCmd.bat'
    if (-not (Test-Path $devcmd)) { throw "VsDevCmd.bat missing: $devcmd" }
    # VS18: no vcvars32/64.bat; VsDevCmd -arch=x86|x64 sets the MSVC env.
    $full = "`"$devcmd`" -arch=$arch -no_logo && $cmd"
    cmd /c $full
    if ($LASTEXITCODE) { throw "cmd failed ($LASTEXITCODE): $cmd" }
}

function CMake-Build([string]$buildDir) {
    if (-not (Test-Path $cmake)) { throw "cmake not found: $cmake" }
    $off = @('-DWEBP_BUILD_ANIM_UTILS=OFF','-DWEBP_BUILD_CWEB=OFF','-DWEBP_BUILD_DWEB=OFF',
             '-DWEBP_BUILD_GIF2WEBP=OFF','-DWEBP_BUILD_IMG2WEBP=OFF','-DWEBP_BUILD_VWEBP=OFF',
             '-DWEBP_BUILD_WEBPINFO=OFF','-DWEBP_BUILD_WEBPMUX=OFF','-DWEBP_BUILD_EXTRAS=OFF')
    # NOTE: the token must be quoted — a bare `-DCMAKE_MAKE_PROGRAM=$ninja` is parsed as a
    # parameter-style argument and the variable is NOT expanded (reproduces on 5.1 and 7).
    & $cmake -S $SrcDir -B $buildDir -G Ninja "-DCMAKE_MAKE_PROGRAM=$ninja" `
        -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=ON @off -DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded
    if ($LASTEXITCODE) { throw "cmake configure failed" }
    & $cmake --build $buildDir --target webp -j 12
    if ($LASTEXITCODE) { throw "cmake build failed" }
}

if ($Fetch) {
    if (-not (Test-Path $SrcDir)) {
        git clone --depth 1 --branch v1.6.0 "$Mirror/webmproject/libwebp" $SrcDir
        if ($LASTEXITCODE) { throw "clone failed" }
    }
    # local patch: static sharpyuv so libwebp_{x86,x64}.dll carry no libsharpyuv.dll dep
    $cml = Join-Path $SrcDir 'CMakeLists.txt'
    $t = [IO.File]::ReadAllText($cml)
    if ($t -match 'add_library\(sharpyuv STATIC ') {
        Write-Host 'sharpyuv STATIC patch already applied'
    } elseif ($t -match 'add_library\(sharpyuv \$\{WEBP_SHARPYUV_SRCS\}\)') {
        $t = $t.Replace('add_library(sharpyuv ${WEBP_SHARPYUV_SRCS})',
                        'add_library(sharpyuv STATIC ${WEBP_SHARPYUV_SRCS})')
        [IO.File]::WriteAllText($cml, $t)
        Write-Host 'sharpyuv STATIC patch applied'
    } else { throw 'unexpected CMakeLists.txt sharpyuv line - inspect manually' }
    # persist patch file for auditors
    git -C $SrcDir diff -- CMakeLists.txt | Set-Content "$PSScriptRoot\sharpyuv-static.patch"
    Write-Host "patch snapshot: $PSScriptRoot\sharpyuv-static.patch"
}

if ($Gcc64) {
    # plain gcc: ensure PATH sees ucrt64
    $env:PATH = "$GccRoot\bin;$env:PATH"
    CMake-Build "$SrcDir\..\lw-build-x64-gcc"
}

if ($Msvc64 -or $Msvc32) {
    foreach ($arch in @(@{on=$Msvc64; a='x64'; d='lw-build-x64-msvc'}, @{on=$Msvc32; a='x86'; d='lw-build-x86-msvc'})) {
        if (-not $arch.on) { continue }
        $buildDir = Join-Path (Split-Path $SrcDir -Parent) $arch.d
        $off = '-DWEBP_BUILD_ANIM_UTILS=OFF -DWEBP_BUILD_CWEB=OFF -DWEBP_BUILD_DWEB=OFF -DWEBP_BUILD_GIF2WEBP=OFF -DWEBP_BUILD_IMG2WEBP=OFF -DWEBP_BUILD_VWEBP=OFF -DWEBP_BUILD_WEBPINFO=OFF -DWEBP_BUILD_WEBPMUX=OFF -DWEBP_BUILD_EXTRAS=OFF -DBUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release -DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded'
        Invoke-Vs $arch.a "`"$cmake`" -S `"$SrcDir`" -B `"$buildDir`" -G Ninja -DCMAKE_MAKE_PROGRAM=`"$ninja`" $off"
        Invoke-Vs $arch.a "`"$cmake`" --build `"$buildDir`" --target webp -j 12"
        # static CRT import check: no VCRUNTIME*.DLL
        $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
        $vsRoot = (& $vswhere -latest -prerelease -property installationPath | Select-Object -First 1)
        $dumpbin = Get-ChildItem (Join-Path $vsRoot 'VC\Tools\MSVC\*\bin\Hostx64\*\dumpbin.exe') | Select-Object -First 1
        $imps = & $dumpbin.FullName /DEPENDENTS (Join-Path $buildDir 'libwebp.dll') | Select-String '\.dll'
        if ($imps -match 'VCRUNTIME|MSVCP|UCRTD?\.DLL') { throw "dynamic CRT leaked into $($arch.d): $imps" }
    }
}

if ($Smoke) {
    # exe is written next to the built libwebp.dll so the loader finds it (exe dir is
    # searched first); never drop build artifacts into this source directory.
    Push-Location $PSScriptRoot
    $env:PATH = "$GccRoot\bin;$env:PATH"
    gcc -O2 -I "$SrcDir\src" "$PSScriptRoot\smoke.c" -L $Smoke -lwebp -o "$Smoke\smoke.exe"
    if ($LASTEXITCODE) { Pop-Location; throw 'smoke compile failed' }
    & "$Smoke\smoke.exe"
    $rc = $LASTEXITCODE
    Pop-Location
    if ($rc) { throw 'smoke test FAILED' }
    Write-Host 'SMOKE PASS'
}

if ($Deploy) {
    foreach ($p in @(@('lw-build-x64-msvc\libwebp.dll','libwebp_x64.dll'), @('lw-build-x86-msvc\libwebp.dll','libwebp_x86.dll'))) {
        $src = Join-Path (Split-Path $SrcDir -Parent) $p[0]
        if (-not (Test-Path $src)) { Write-Warning "missing $src"; continue }
        Copy-Item $src (Join-Path $deployTarget $p[1]) -Force
        $v = (Get-Item (Join-Path $deployTarget $p[1])).VersionInfo.FileVersion
        Write-Host ("deployed {0} -> {1} (FileVersion {2})" -f $p[1], $deployTarget, $v)
    }
}
