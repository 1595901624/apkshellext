# ApkShellExt Build Guide (compile_guidance.md)

> This document records the **complete, actually-executed** procedure for going from a
> fresh clone to a working `ApkShellext2.dll` (with 19 localized satellite assemblies)
> and `apkshellextTest.exe`. Every command was run and verified on 2026-11-20 in
> `E:\src\apkshellext` (ApkShellExt **3.4.5**, HEAD `076f182`). Copy them verbatim,
> adjusting drive/paths.
>
> The two project files in this checkout were **modernized to SDK-style csproj**
> (§5), which is what makes both build routes work with **zero extra MSBuild
> properties**. Companion document: [中文对照版 compile_guidance.zh-CN.md](compile_guidance.zh-CN.md).

## 0. Summary

| Item | Value |
|---|---|
| Main artifact | `ApkShellext2\bin\Release\ApkShellext2.dll` (3.4.5.0, strong-named `Pkt=fb82661d1c53098c`, local key) |
| Satellites | `bin\Release\{ar-SA,…,zh-CN,zh-TW}\ApkShellext2.resources.dll` × 19 |
| Test app | `apkshellextTest\bin\Release\apkshellextTest.exe` |
| Packages | `bin\Release\ApkShellext2.7z` + 19 × `<locale>.7z` (needs 7-Zip; postbuild packs automatically) |
| Route A | .NET SDK **10.0.404** standalone: `dotnet build ApkShellext2\ApkShellext2.csproj -c Release` — one line, no flags |
| Route B | Visual Studio 2022/2026 with *.NET desktop development*: `msbuild -t:restore` + `msbuild -p:Configuration=Release` — no flags either |
| Key gotchas | ① signing key `apkshellext2_hy.snk` is not in the repo — generate one (§4.2); ② a machine can have **zero NuGet sources** configured and restore fails with NU1100 (§4.1); ③ outputs built with the dotnet CLI additionally ship `System.Resources.Extensions.dll` (runtime dependency of their preserialized resources — postbuild packs it automatically, §8) |

## 1. Environment and the two build routes

The project targets **.NET Framework 4.8** (`net48`) and both projects are
**SDK-style csproj** files (`<Project Sdk="Microsoft.NET.Sdk">`) with `LangVersion=10.0`.
Consequence: any reasonably modern MSBuild can build them — no Visual Studio required.

- **Route A:** standalone .NET SDK (≥ 8; verified on 10.0.404), plain
  `dotnet build`. No extra properties, no shims, no pregeneration steps.
- **Route B:** Visual Studio ("*.NET desktop development*" workload, which carries
  MSBuild + the .NET SDK + Roslyn + NuGet) or Build Tools. Plain `msbuild` commands,
  also with zero extra properties. Works on VS 2026 (18) and VS 2022 (17.x) alike.
  The .NET Framework 4.8 targeting pack helps but is not strictly required on
  every machine (see §4.4).

Both routes produce a functionally identical binary; the one measurable difference
(resource serialization mode → `System.Resources.Extensions.dll` in the output) is
explained in §8.

Reference environment for the example paths below (adjust to your machine):
Windows x64, repo at `E:\src\apkshellext`, toolchain at `E:\src\apkshellext-dep`,
7-Zip at the non-standard path
`D:\Software\7-Zip\` (discoverable via registry `HKLM:\SOFTWARE\7-Zip` → `Path`;
postbuild finds it automatically). v4.8 reference assemblies present at
`C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8`.

## 2. Clone the repository

Default: clone straight from GitHub.

```powershell
git clone https://github.com/nkh0472/apkshellext.git E:\src\apkshellext
```

Only if github.com is unreachable or too slow, use the githubfast mirror instead
(same rule applies to the submodules in §3):

```powershell
git clone https://githubfast.com/nkh0472/apkshellext.git E:\src\apkshellext
```

## 3. Submodules (5 of them)

`.gitmodules` points at github.com, so the plain command works whenever the clone
above was direct:

```powershell
git -C E:\src\apkshellext submodule update --init --recursive --jobs 4
```

If the network cannot reach github.com, do the same thing through a one-shot mirror
rewrite (`-c` keeps the `insteadOf` mapping to this command only):

```powershell
git -C E:\src\apkshellext -c url."https://githubfast.com/".insteadOf="https://github.com/" submodule update --init --recursive --jobs 4
```

All five submodules are genuinely compiled into the build:

| Submodule | Upstream | Notes |
|---|---|---|
| `ApkShellext2\SharpZipLib` | icsharpcode/SharpZipLib @ `33f64eb` | glob-compiles `src\ICSharpCode.SharpZipLib\**` (excludes AssemblyInfo/GZip/Lzw/Tar/Core\ExactMemoryPool.cs) |
| `ApkShellext2\PlistCS` | kkguo/PlistCS @ `0fc69e7` | source-level reference |
| `ApkShellext2\PNGDecrush` | nkh0472/PNGDecrush @ `e926128` | source-level reference |
| `ApkShellext2\SVG` | vvvv-github/SVG **v3.5.0** @ `9d564f4` | source-level reference; its **source generator runs automatically** as a compiler analyzer (§5) — no manual step |
| `ApkShellext2\WebP-wrapper` | JosePineiro/WebP-wrapper @ `79e4234` | source-level reference (runtime additionally needs `LibWebP\libwebp_x86/x64.dll`, committed in-repo — no build needed) |

Notes:
- **SharpShell is not a submodule** — its sources (2.7.1) are vendored directly under
  `ApkShellext2\SharpShell\` and compiled by the csproj (minus dead code, §5.1).
- Building the repo dirties the SVG submodule with `bin\`/`obj\` (the generator project
  compiles there). Keep `git status` clean by adding a *local* ignore inside the
  submodule: append `bin/` and `obj/` to
  `E:\src\apkshellext\.git\modules\ApkShellext2\SVG\info\exclude` (find the exact path
  with `git -C ApkShellext2\SVG rev-parse --absolute-git-dir`).

## 4. Toolchain and one-time setup

### 4.1 Install the .NET SDK (Route A only) — and make sure NuGet has a source

```powershell
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'E:\src\apkshellext-dep\dotnet-install.ps1' -UseBasicParsing
& 'E:\src\apkshellext-dep\dotnet-install.ps1' -Channel 10.0 -InstallDir 'E:\src\apkshellext-dep\dotnet-sdk-10' -NoPath
# Channel 10.0 resolves to 10.0.404 as of this writing. Any SDK >= 8 works (analyzer needs Roslyn >= 4.8).
```

**Check the NuGet sources once.** A user-level `NuGet.Config` whose `<packageSources>`
section exists but is *empty* silently disables the implicit nuget.org default, and
restore dies with `error NU1100: 无法解析 net48 的 "System.Resources.Extensions (>= 6.0.0)"`
(or the first PackageReference it tries):

```powershell
& E:\src\apkshellext-dep\dotnet-sdk-10\dotnet.exe nuget list source   # must show nuget.org
# If it shows nothing:
& E:\src\apkshellext-dep\dotnet-sdk-10\dotnet.exe nuget add source https://api.nuget.org/v3/index.json -n nuget.org
```

This bites both routes (VS shares the same user config). The old repo layout needed no
restore at all (HintPath DLLs in `packages\`), so a source-less machine never noticed —
the SDK-style project restores ExCSS/System.Memory/etc. from nuget.org instead (§5.2).

### 4.2 Signing key snk (generate locally — the wrong key breaks the VS route)

The csproj sets `SignAssembly=true`, `AssemblyOriginatorKeyFile=apkshellext2_hy.snk`.
The official private key is not published with the repo and `*.snk` is gitignored.
The README says: *"Release builds sign with `ApkShellext2\apkshellext2_hy.snk` —
generate your own key with `sn -k`"*. **Generating your own key is enough** (public key
differs from official releases — the output is for local use only and can never
impersonate the official signed build).

`sn -k` (a Windows SDK tool) works, but you don't need any SDK for it — stock Windows
PowerShell 5.1 can produce the *same* key. Use this exact form:

```powershell
& C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -Command "
  `$cp = New-Object Security.Cryptography.CspParameters
  `$cp.ProviderType = 1   # PROV_RSA_FULL
  `$cp.KeyNumber    = 2   # AT_SIGNATURE  <-- this is the part that matters
  `$rsa = New-Object Security.Cryptography.RSACryptoServiceProvider(`$cp)
  [IO.File]::WriteAllBytes('E:\src\apkshellext\ApkShellext2\apkshellext2_hy.snk', `$rsa.ExportCspBlob(`$true))"
# 596 bytes; header should read 07 02 00 00 00 24 00 00 "RSA2" (aiKeyAlg=0x2400 CALG_RSA_SIGN)
```

**Trap:** the naive
`New-Object System.Security.Cryptography.RSACryptoServiceProvider 1024` defaults to a
*key-exchange* key (`aiKeyAlg=0xa400`). Roslyn/csc happily signs with it (dotnet route
all green), but the classic `al.exe` that signs **satellite assemblies under Visual
Studio's MSBuild** refuses: `ALINK : error AL1078 … 未知错误(8013141e)`. If you see
AL1078 on the VS route, regenerate the snk (gotcha 13) before suspecting your VS
install.

### 4.3 7-Zip (optional; only affects the postbuild packaging step)

`postbuild.cmd` searches: `%ProgramFiles%\7-Zip` and `%ProgramFiles(x86)%\7-Zip` →
`HKLM:\SOFTWARE\7-Zip` (InstallPath and Path values, **in both the 64-bit and 32-bit
registry views via `reg /reg:64` / `/reg:32`** — classic `Bin\MSBuild.exe` is 32-bit and
a bare query would miss a 64-bit install, gotcha 14) → `where 7z`. If nothing is
found it only prints a **WARNING** and skips packaging — the DLL output is unaffected.
To produce the `.7z` packages, install 7-Zip by any means, e.g.:

```powershell
winget install 7zip.7zip
```

### 4.4 .NET Framework 4.8 reference assemblies

Needed by every net48 build. Present on current Windows at
`C:\Program Files (x86)\Reference Assemblies\...\.NETFramework\v4.8`, or via the VS
component *".NET Framework 4.8 SDK / Targeting Pack"* (bundled in the *.NET desktop
development* workload). If neither exists, the SDK falls back to the
`Microsoft.NETFramework.ReferenceAssemblies` NuGet package automatically in some SDK
versions; do not rely on that — check the folder exists.

## 5. What this checkout changed vs. upstream (the modernization)

Upstream ships **old-style** csproj files. Building them with `dotnet msbuild` needs
three ugly workarounds (SVG source-generator pregeneration, a satellite-assembly shim
for `MSB4064`, manual NuGet package backfill). This checkout rewrites both csproj files
as **SDK-style** instead, which removes all three at the root — that is why every
command in §6–§7 is flag-free. All of it lands on top of upstream 3.4.5
(HEAD `076f182`) as a set of focused commits — build-system migration
(`ApkShellext2.sln`, both csproj files, `Resources\postbuild.cmd`), dead-code removal,
donation-UI removal, typo fixes, plus this documentation. As of writing they are
**committed locally but not pushed** — push to your remote (or carry this checkout)
before expecting a fresh clone elsewhere to build with the flag-free commands below.

### 5.1 Legacy workaround → SDK-style replacement

| Upstream / legacy need | Now |
|---|---|
| SVG v3.5.0 element metadata generated by hand before the build (`pregen.ps1` → `SVG\Source\Generated\*.g.cs`) | `<ProjectReference Include="SVG\Generators\Svg.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />` — the same upstream generator runs **inside Roslyn** on every compile, on both toolchains |
| Satellites crash under coreclr MSBuild (`MSB4064` AL/ToolPath) → external `dotnet-satellites.targets` shim + 2 command-line flags | SDK-style projects get `GenerateSatelliteAssembliesForCore=true` automatically under coreclr (Csc-based generation); FullFramework MSBuild uses real `al.exe`. Nothing to pass |
| `packages\System.Runtime.CompilerServices.Unsafe.6.0.0` fetched by hand (HintPath project, no restore) | `PackageReference`s restore everything: ExCSS 4.2.3, System.Memory 4.5.5 (drags Buffers + Numerics.Vectors), System.Runtime.CompilerServices.Unsafe 6.0.0 (direct ref overrides the transitive 4.5.3 — identical to the old HintPath resolution), and System.Resources.Extensions 6.0.0 **only when** building under coreclr |
| `-p:GenerateResourceUsePreserializedResources=true` typed by the user | inside the csproj: `Condition="'$(MSBuildRuntimeType)' == 'Core'"` — dotnet CLI uses preserialized resources, VS uses classic ones, automatically (see §8 for the one runtime consequence) |
| `apkShellextService` dead entry in the .sln (directory never existed) | entry deleted from the .sln |
| csproj `Content`-references `SharpShellNativeBridge32/64.dll`, which upstream deleted in `4f30b7c`; the SharpPropertySheet property-page machinery referencing them is dead code | those `Content` items are gone, and the dead consumer directories `NativeBridge\`, `SharpPropertySheet\` and `SharpDeskBand\` have been deleted from the tree; `Helpers\RegAsm.cs` and the vendored `Properties\AssemblyInfo.cs` remain on disk but were never compile inputs. No compiled file references those namespaces |

### 5.2 Other modernization notes

- The legacy `packages\` folder is now **unused** (leftover; harmless). Do not re-add
  HintPath references.
- `AppendTargetFrameworkToOutputPath=false` keeps the flat `bin\Release` layout that the
  install scripts, `postbuild.cmd` and this guide all assume (SDK default would be
  `bin\Release\net48\`).
- `GenerateAssemblyInfo=false`: `Properties\AssemblyInfo.cs` stays the single source of
  version/Guid/ComVisible attributes.
- `EnableDefaultCompileItems/EmbeddedResourceItems/NoneItems=false` with explicit globs —
  the vendored submodule trees contain many files that must *not* be compiled
  (their own AssemblyInfo, samples, tools, WinUI/WPF variants).
- The legacy `Debug2` configuration is kept (`Configurations=Debug;Debug2;Release`); it
  behaves like Debug. The generator ProjectReference maps it to the generator's own
  Debug output via `SetConfiguration`.

## 6. Build the main project

### Route A — dotnet CLI

```powershell
Set-Location E:\src\apkshellext
E:\src\apkshellext-dep\dotnet-sdk-10\dotnet.exe build ApkShellext2\ApkShellext2.csproj -c Release --nologo
```

One line. Restore runs implicitly. Expected: `EXIT=0` with only the seven pre-existing
code warnings (CS0618 obsolete SVG API ×2, CS0219 unused variable ×4, CS0169 unused
field) and `ApkShellext2 -> …\bin\Release\ApkShellext2.dll`, followed by the 7z
packaging step (§8).

### Route B — Visual Studio / Build Tools MSBuild

```powershell
# Locate MSBuild (skip if you know the path):
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msb = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1

Set-Location E:\src\apkshellext
& $msb ApkShellext2\ApkShellext2.csproj -t:restore -p:Configuration=Release -nologo -v:m
& $msb ApkShellext2\ApkShellext2.csproj -p:Configuration=Release -nologo -v:m
```

Two commands (restore is *not* implicit for classic MSBuild), still zero custom
properties. Mind the colon: classic MSBuild wants `-v:m`, not `-v m` (gotcha 3). If
restore/build fails with `MSB4236 找不到指定的 SDK“Microsoft.NET.Sdk”`, your VS install
is broken — gotcha 2. `Bin\MSBuild.exe` is the **32-bit** launcher (64-bit lives in
`Bin\amd64\`); either works here — the postbuild 7-Zip discovery is explicitly
bitness-proof (gotcha 14). VS 18 additionally carries its own SDK under
`C:\Program Files\dotnet\sdk\`, so Route B needs nothing from Route A.

The whole solution (main + test) also builds in one go on either route:
`dotnet build ApkShellext2.sln -c Release` / `msbuild ApkShellext2.sln -t:restore` +
`msbuild ApkShellext2.sln -p:Configuration=Release`.

## 7. Build the test project

```powershell
# Route A:
E:\src\apkshellext-dep\dotnet-sdk-10\dotnet.exe build apkshellextTest\apkshellextTest.csproj -c Release --nologo
# Route B: the same two msbuild lines pointed at apkshellextTest\apkshellextTest.csproj
```

It is a WinForms app referencing the main project (plus one linked file
`..\ApkShellext2\ApkChunk.cs`), unsigned, with no NuGet dependencies. Debug builds work
identically (`-c Debug` / `-p:Configuration=Debug`); the Debug output additionally gets
`debug.bat` (a dev helper copied only in Debug, §8). Never look for
`apkShellextService` — its dead .sln entry was deleted here and never existed on disk.

## 8. Publish layout, postbuild and deployment notes

**The layout is assembled by MSBuild — postbuild only packages.**

- **MSBuild side** (`ApkShellext2.csproj`): `install.bat`, `install_user.bat`,
  `uninstall.bat`, `uninstall_user.bat`, `restart_explorer.bat` and both `libwebp_*.dll`
  are `Content` items with `CopyToOutputDirectory=PreserveNewest`; `debug.bat` likewise
  under `Condition="'$(Configuration)'=='Debug'"`. `CopyFilesToOutputDirectory` copies
  them before any packaging runs, so `bin\Release` is already a complete publish folder.
  Every item carries `TargetPath=<bare filename>` — without it the copy recreates the
  `Resources\`/`LibWebP\` source subfolders inside the output (gotcha 5).
- **Packaging hook**: implemented as `<Target Name="PackageWith7z" AfterTargets="PostBuildEvent">`
  calling the script via `Exec`, *not* the legacy `<PostBuildEvent>` property — in an
  SDK-style project a property assignment expands its macros at **evaluation time**,
  when `$(ProjectDir)`/`$(TargetDir)` are still undefined, so the old one-liner bakes
  into `call "Resources\postbuild.cmd" "" ""` (gotcha 4).
- **`Resources\postbuild.cmd`** (Release-only; Debug exits immediately): deletes a stale
  `debug.bat`, locates `7z.exe` (§4.3), deletes stale `*.7z`, then packs
  `ApkShellext2.7z` = `ApkShellext2.dll` + `*.bat` + `libwebp_*.dll` + `ExCSS.dll` +
  `System.Memory.dll` + `System.Buffers.dll` + `System.Runtime.CompilerServices.Unsafe.dll`
  **plus `System.Resources.Extensions.dll` if present** — see below — and one
  `<locale>.7z` per language folder. No 7-Zip ⇒ WARNING + skip, build still succeeds.

**The one route difference — `System.Resources.Extensions.dll`.** Under coreclr MSBuild
(dotnet CLI) the csproj enables preserialized resource generation; the resulting
`.resources` blobs embed types from that assembly, so a **dotnet-CLI build needs
`System.Resources.Extensions.dll` next to `ApkShellext2.dll` at runtime** (verified:
without it, reading the icon-bearing `NonLocalizeResources` throws
`FileNotFoundException: System.Resources.Extensions, Version=4.0.0.0, Pkt=cc7b13ffcd2ddd51`
the first time a non-string resource is touched — string lookups like `btnOK` still
work, which makes the fault easy to miss). A Visual Studio (FullFramework) build uses
classic BinaryFormatter-style resources, never references the package, and produces no
such file — matching upstream packages exactly. `postbuild.cmd` adapts automatically
(package it if present), so each route's `.7z` is self-consistent: **13 files** from
the dotnet route, **12** from the VS route. Do not hand-copy `System.Resources.Extensions.dll`
into a VS-route output folder — it is not needed there.

Deployment (on the target machine, as the user who owns the shell extension): extract
`ApkShellext2.7z` (+ the wanted `<locale>.7z` so the culture folder sits next to the
DLL), then run `install.bat` (elevated, machine-wide) or `install_user.bat` (per-user;
both register via regasm and NGEN the assembly — 3.4.5 fixed NGEN in these scripts).
`restart_explorer.bat` reloads the shell. Uninstall with the matching `uninstall*.bat`.
Because the local snk differs from upstream's, never mix DLLs from a local build with an
official package in the same GAC/registration state.

## 9. Verifying the build

1. **Satellite directories:** `bin\Release` must contain all 19 culture folders
   (`ar-SA de-DE el-GR es-ES fa-IR fr-FR he-IL it-IT ja-JP ko-KR my-MM nl-NL pl-PL pt-PT
   ru-RU tr-TR uk-UA zh-CN zh-TW`), each with `ApkShellext2.resources.dll`.
2. **Identity via reflection** — run under **Windows PowerShell 5.1**
   (`C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`); PowerShell 7 (.NET
   Core) cannot reflect these .NET Framework assemblies properly. Format the token bytes
   with `{0:x2}` — `-join`-ing the raw byte array yields decimal digits, not the hex
   token:

   ```powershell
   $ma = [Reflection.Assembly]::LoadFrom('E:\src\apkshellext\ApkShellext2\bin\Release\ApkShellext2.dll')
   $ma.GetName() | Format-List Name, Version   # ApkShellext2  3.4.5.0
   (($ma.GetName().GetPublicKeyToken() | % { '{0:x2}' -f $_ }) -join '')   # fb82661d1c53098c  (local key)
   $sat = [Reflection.Assembly]::LoadFrom('E:\src\apkshellext\ApkShellext2\bin\Release\zh-CN\ApkShellext2.resources.dll')
   $sat.GetName() | Format-List Version, CultureName   # 3.4.5.0, zh-CN — same Pkt as main
   ```

3. **ResourceManager round-trip** (proves satellites are discoverable, not just present):

   ```powershell
   $rm = New-Object System.Resources.ResourceManager('ApkShellext2.Properties.Resources', $ma)
   $rm.GetString('btnOK', [cultureinfo]'zh-CN')              # → 确定   (from satellite)
   $rm.GetString('btnOK', [cultureinfo]::InvariantCulture)   # → OK     (neutral, inside main dll)
   ```

4. **Complete publish layout in `bin\Release`:** `ApkShellext2.dll(.pdb)`, the
   five bats, `libwebp_x86/x64.dll`, copy-local NuGet DLLs (`ExCSS`, `System.Memory`,
   `System.Buffers`, `System.Numerics.Vectors`, `System.Runtime.CompilerServices.Unsafe`;
   `System.Resources.Extensions` only on the dotnet route) and — when 7-Zip was found —
   `ApkShellext2.7z` (~959 KB) + 19 × `<locale>.7z` (~5 KB each). `7z l` must list
   **exactly 13 files (dotnet route) or 12 (VS route)** at the archive root, and no
   `Resources\`/`LibWebP\` subfolders may exist in `bin\Release`.
5. **Debug layout:** `bin\Debug` root has `debug.bat` + bats + libwebp and **no `.7z`**.

## 10. Gotchas checklist

1. **NU1100 on restore** → the machine has **zero NuGet sources** (an empty
   `<packageSources>` in user `NuGet.Config` kills the implicit default).
   `dotnet nuget list source` must show nuget.org; add it per §4.1.
2. **`MSB4236 / MSB4276: 无法解析 SDK“Microsoft.NET.Sdk”` from VS's MSBuild** (often with
   a silent `vswhere`) = broken/partial VS install — `MSBuild\Sdks` is empty. Proper fix:
   VS Installer → **Repair**, or reinstall with the *.NET desktop development* workload.
   After Repair or a clean reinstall, vswhere resolves and restore/build run with zero
   extra parameters.
3. **Classic MSBuild switches take a colon**: `-v:m`, `-p:Configuration=Release`;
   `-v m` (space form, fine for `dotnet build`) is rejected by `MSBuild.exe`.
4. **Do not re-implement the packaging hook as a `<PostBuildEvent>` property** — in
   SDK-style projects its macros expand at evaluation time (empty `$(TargetDir)` etc.)
   and the script silently receives `"" ""`. Keep the `AfterBuild`-style `Exec` target.
5. **`Content` + `CopyToOutputDirectory` recreates source subfolders** unless every item
   sets `TargetPath=<bare filename>`; and the 7z step's `*.bat` wildcard *silently
   ignores* a pattern matching nothing — the archive builds without bats and nothing
   errors. After touching layout items, check `7z l` and the output tree.
6. **Route-dependent runtime file**: dotnet-CLI-built outputs need
   `System.Resources.Extensions.dll` at runtime (§8). postbuild packs it automatically;
   never strip it from a dotnet-route archive, never add it to a VS-route deploy.
7. **snk**: gitignored; generate locally (§4.2). Public key ≠ official builds.
8. **Legacy leftovers are intentional**: the `packages\` folder is unused now; the
   NativeBridge native DLLs are missing upstream by design (`4f30b7c`) and their dead
   consumer code has been deleted from the tree (§5.1). Do not “fix” either.
9. **Building dirties the SVG submodule** with `bin\`/`obj\` → add them to the
   submodule's local `info/exclude` (§3 note) or `git status` lies.
10. **Renaming/moving the SDK folder fails with “Access denied”** while MSBuild
    node-reuse workers are alive (`MSBuild.exe … /nodemode:2 /nodereuse:True` started
    from the SDK dir; `dotnet build-server shutdown` does not always catch them).
    Find the holder with `Get-CimInstance Win32_Process | ? CommandLine -match '<sdk-root>'`
    and stop it first.
11. **`dotnet` on PATH may be runtime-only.** Verify with `dotnet --version`; if it
    reports no SDK, use the installed SDK's full path (as all commands here do).
12. **Four files are intentionally not upstream** (§5): `.sln`, both csproj
    files, `postbuild.cmd`. Reverting any of them to its upstream version re-introduces
    the legacy workarounds (flags, shim, pregen, manual packages).
13. **`ALINK : error AL1078 对程序集签名时出错 - 未知错误(8013141e)` on the VS route** =
    the snk is a *key-exchange* blob (`aiKeyAlg=0xa400`, what a bare
    `RSACryptoServiceProvider 1024` produces). csc ignores the field, `al.exe` refuses it.
    Regenerate with `sn -k` or the §4.2 PowerShell snippet (`KeyNumber=Signature`,
    header `… 00 24 00 00 "RSA2"`). Not a VS-install problem, not a repo problem.
14. **VS's command-line `Bin\MSBuild.exe` is 32-bit**, so its Exec children see the
    WOW64-redirected registry (`HKLM\SOFTWARE\7-Zip` “missing”) and `%ProgramFiles%`
    pointing at `Program Files (x86)` — a bare `reg query` in postbuild then silently
    skips packaging while the build stays green. `postbuild.cmd` therefore queries both
    views explicitly (`/reg:64`, `/reg:32`); keep those flags if you touch the script.
    (The VS IDE itself builds in a 64-bit engine, which is why this only bites
    command-line builds of the x86 launcher.)

## Appendix A — one-shot reference script (Route A)

```powershell
$R = 'E:\src\apkshellext'
$DOTNET = 'E:\src\apkshellext-dep\dotnet-sdk-10\dotnet.exe'

# --- 0. prerequisites (once): submodules §3, nuget source §4.1, snk §4.2 ---
& $DOTNET nuget list source | Select-String nuget.org -Quiet | % { if (-not $_) { throw 'no nuget.org source - see §4.1' } }
if (-not (Test-Path "$R\ApkShellext2\apkshellext2_hy.snk")) { throw 'generate the snk first - see §4.2' }

# --- 1. main + test (restore implicit; analyzer generator runs inside compile) ---
Set-Location $R
& $DOTNET build ApkShellext2\ApkShellext2.csproj     -c Release --nologo; if ($LASTEXITCODE) { throw 'main build failed' }
& $DOTNET build apkshellextTest\apkshellextTest.csproj -c Release --nologo; if ($LASTEXITCODE) { throw 'test build failed' }

# --- 2. verify (must run under Windows PowerShell 5.1, see §9) ---
$sats = Get-ChildItem "$R\ApkShellext2\bin\Release" -Directory | Where-Object Name -cmatch '^[a-z]{2}-'
"satellites: $($sats.Count) (expect 19)"
$check = @'
$ma = [Reflection.Assembly]::LoadFrom('E:\src\apkshellext\ApkShellext2\bin\Release\ApkShellext2.dll')
$n = $ma.GetName()
"MAIN: $($n.Name) $($n.Version) pkt=$((($n.GetPublicKeyToken() | % { '{0:x2}' -f $_ }) -join ''))"
$rm = New-Object System.Resources.ResourceManager('ApkShellext2.Properties.Resources', $ma)
"btnOK zh-CN = " + $rm.GetString('btnOK', [cultureinfo]'zh-CN')      # 确定
"btnOK inv   = " + $rm.GetString('btnOK', [cultureinfo]::InvariantCulture)  # OK
'@
$tmp = "$env:TEMP\apkcheck.ps1"; Set-Content $tmp $check -Encoding UTF8
& C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -ExecutionPolicy Bypass -File $tmp
```

## Appendix B — one-shot reference script (Route B)

```powershell
$R = 'E:\src\apkshellext'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msb = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msb) { throw 'MSBuild not found - repair VS / install the .NET desktop workload (§10 gotcha 2)' }

Set-Location $R
& $msb ApkShellext2.sln -t:restore            -p:Configuration=Release -nologo -v:m; if ($LASTEXITCODE) { throw 'restore failed' }
& $msb ApkShellext2.sln -p:Configuration=Release              -nologo -v:m; if ($LASTEXITCODE) { throw 'build failed' }

# verify the VS-route specifics: NO System.Resources.Extensions.dll in the output, 12-file archive
Test-Path "$R\ApkShellext2\bin\Release\System.Resources.Extensions.dll"   # expect False
& 'D:\Software\7-Zip\7z.exe' l "$R\ApkShellext2\bin\Release\ApkShellext2.7z"   # expect 12 files (adjust 7z path)
```

---

*Companion document: [中文对照版 compile_guidance.zh-CN.md](compile_guidance.zh-CN.md).*
