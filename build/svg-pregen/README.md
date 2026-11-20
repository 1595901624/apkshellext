# SVG 3.5.0 source-generated code pre-generation

The repo compiles `ApkShellext2/SVG/Source/**` straight into `apkshellext2.dll`.
Svg 3.5.0 depends on Roslyn source generators (`Generators/Svg.Generators.csproj`):
without them `SvgElements.ElementNames` / the `SvgElementFactory` partial are missing
(compiles fail with CS0103/CS0234 on `SvgElements`).

MSBuild (classic VS/old-style csproj path) would run the analyzer itself, but the
analyzer cannot be hosted by this repo's coreclr verification toolchain, so we run
the generator ONCE with a modern SDK and check the output in... actually the output
is gitignored — rerun after every SVG submodule bump.

## Procedure
```powershell
.\pregen.ps1
```
`pregen.ps1` copies the submodule to `X:\apkshellext_upstream\svg-pregen` (Nerdbank
GitVersioning 3.3.37 crashes on shallow submodules — a plain folder avoids it),
drops a temp `version.json`, builds `Source\Svg.csproj -f net472` with
`dotnet-sdk-10\dotnet.exe` + `/p:RestoreSources=https://api.nuget.org/v3/index.json`
(user NuGet.Config on this machine has no sources), then robocopies
`obj\...\generated\**\*.g.cs` back to `ApkShellext2\SVG\Source\Generated\`
(gitignored at .gitignore:365 — NOT committed).

## Consuming side
`ApkShellext2.csproj` compiles `SVG\Source\**\*.cs` (Generated included — the
`<Compile Remove="SVG\Source\Generated\**">` guard was removed on 2026-11-19), and
defines `NETFULL;NETFRAMEWORK;NET48;NET462_OR_GREATER` with `LangVersion 10`.
References: ExCSS 4.2.3 (net48 lib), System.Memory/Buffers/Vectors/Unsafe 4.5.x
facades, System.Net.Http, System.Web, WindowsBase, System.Xaml.
svg11.dtd is embedded as `Svg.Resources.svg11.dtd` (logical name required by
`SvgDtdResolver.cs:40`); runtime smoke verified it loads (199623 B).
