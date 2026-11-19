# Svg 3.5.0 source-generator pre-build (run BEFORE MSBuild of ApkShellext2).
# Why: host compiles Svg sources directly into apkshellext2.dll, so the
# compile-time generator (Generators\Svg.Generators) cannot run inside the host
# build (old-style csproj has no analyzer support). We build Svg once with the
# .NET SDK and keep the emitted sources (EmitCompilerGeneratedFiles ->
# Source\Generated) as wildcard-compiled inputs of the host project.
#
# The build runs on a COPY under $WorkDir, not in the submodule: the v3.5.0 tag
# ships no version.json (NerdBank.GitVersioning reads it from git history) and
# the submodule is shallow (depth 1) — NB.GV's libgit2 history walk throws
# MSB4018 on a grafted repo. On a detached copy NB.GV falls back to the working
# version.json, and the submodule stays pristine.
#
# Output: X:\apkshellext\ApkShellext2\SVG\Source\Generated\**\*.g.cs
# Requires: dotnet SDK >= 9 (Svg.csproj also targets net9.0; SDK8 hits NETSDK1045)
# and nuget.org reachability (RestoreSources is passed per-process — the
# machine's global NuGet.Config may have no sources configured).
param(
    [string]$Dotnet  = 'X:\apkshellext_upstream\dotnet-sdk-10\dotnet.exe',
    [string]$SvgDir  = 'X:\apkshellext\ApkShellext2\SVG',
    [string]$WorkDir = 'X:\apkshellext_upstream\svg-pregen'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Dotnet)) { throw "dotnet not found: $Dotnet (extract SDK zip first)" }

# 1. mirror Source\ + Generators\ (P2P analyzer ref is ..\Generators\...)
robocopy $SvgDir $WorkDir /MIR /XD .git bin obj /XF .gitignore /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
Set-Content (Join-Path $WorkDir 'version.json') '{"version":"3.5.0"}' -Encoding ascii

# 2. build net472 only (TFM list is global; Generators stays netstandard2.0)
& $Dotnet build (Join-Path $WorkDir 'Source\Svg.csproj') `
    -f net472 -c Release `
    /p:GeneratePackageOnBuild=false `
    /p:RestoreSources=https://api.nuget.org/v3/index.json `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "pregen build failed ($LASTEXITCODE)" }

# 3. copy generated sources back into the submodule (git-ignored path)
$genSrc = Join-Path $WorkDir 'Source\Generated'
$genDst = Join-Path $SvgDir  'Source\Generated'
$gen = Get-ChildItem $genSrc -Recurse -Filter *.cs -ErrorAction SilentlyContinue
if (-not $gen) { throw 'no generated files produced' }
robocopy $genSrc $genDst /MIR /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy back failed ($LASTEXITCODE)" }
(Get-ChildItem $genDst -Recurse -Filter *.cs | ForEach-Object { '  ' + $_.Name + '  ' + $_.Length + ' B' })
Write-Host 'Pregen OK.'
