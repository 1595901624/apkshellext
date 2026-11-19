# libwebp 1.6.0 native DLL rebuild

`ApkShellext2/LibWebP/libwebp_x64.dll` / `libwebp_x86.dll` shipped in this repo
(2026 refresh: moved out of the WebP-wrapper submodule into the main repo so the
binaries are version-tracked by apkshellext itself)
are built from **official libwebp v1.6.0 sources** with one local patch
(`sharpyuv-static.patch`): libsharpyuv is linked **statically** into libwebp so that each
bitness DLL is self-contained. This matters because both `libwebp_x86.dll` and
`libwebp_x64.dll` sit in the *same* install directory; an official build would add a
`libsharpyuv.dll` dependency whose fixed file name cannot serve two bitnesses at once.

Why 1.6.0: shipped DLLs were 1.0.3 (2013). Since then: CVE-2023-4863 (libwebp ≤1.3.1,
heap overflow in libwebp decoder, fixed 1.3.2), CVE-2023-51294 (≤1.3.2, fixed 1.3.3+),
plus years of fuzz fixes (1.4.0/1.5.0/1.6.0). The C# wrapper (`WebPWrapper.cs`) only calls
stable plain-C exports (`WebPGetInfo`, `WebPDecodeBGRInto`, `WebPDecodeBGRAInto`,
`WebPGetFeatures`); the decoder ABI major byte (0x02) is unchanged, verified against 1.6.0
headers and by the runtime smoke test (`smoke.c`).

## Prerequisites
- Git (for cloning sources) — GitHub via mirror if needed.
- CMake + Ninja + a C compiler:
  - x64: MSYS2 ucrt64 gcc **or** MSVC (any of the two)
  - x86: MSVC (cl.exe for x86, e.g. via a VS Developer prompt)
- Run from a normal PowerShell. For MSVC builds either start a `vcvars64.bat`/`vcvars32.bat`
  prompt first, or let the script call `build` (it auto-detects via vswhere).

## Usage
```powershell
# NOTE: every mode takes -SrcDir if the source tree is not ./libwebp-1.6.0-src, e.g.
#   .\build-libwebp.ps1 -Msvc32 -SrcDir X:\apkshellext_upstream\libwebp-1.6.0-src

# clone + patch v1.6.0 into ./libwebp-1.6.0-src (idempotent)
.\build-libwebp.ps1 -Fetch

# build x64 with MSYS2 gcc (default path D:\msys64\ucrt64) 
.\build-libwebp.ps1 -Gcc64

# build x86 with MSVC (auto-detects VS, or pass -Vcvars 'C:\...\vcvars32.bat')
.\build-libwebp.ps1 -Msvc32

# build x64 with MSVC too (optional, for symmetry)
.\build-libwebp.ps1 -Msvc64

# decode smoke test (compiles smoke.c against the built DLL, runs it)
.\build-libwebp.ps1 -Smoke <build-dir> <compiler>

# copy results into ApkShellext2\LibWebP\ (main repo)
.\build-libwebp.ps1 -Deploy   # lw-build-x64-msvc\libwebp.dll -> LibWebP\libwebp_x64.dll etc.
                              # (-Gcc64's lw-build-x64 is gcc output; -Deploy expects the -msvc dirs)
```

After `-Deploy`, verify exports (CMake builds carry no VERSIONINFO resource, so FileVersion is
empty by design): `dumpbin /EXPORTS ..\..\ApkShellext2\LibWebP\libwebp_x64.dll | findstr /I "WebPDecodeBGRInto WebPGetInfo"`
— and optionally check the build dir's `libwebp.dll` timestamp. The ABI story is in "Pinned facts" below.

## Pinned facts (verified 2026-11-19 against v1.6.0)
- `WEBP_DECODER_ABI_VERSION = 0x0210`; compat rule `(a>>8)!=(b>>8)` — wrapper's
  compiled-in `0x0208` passes (same major byte).
- gcc x64 build imports only KERNEL32 + api-ms-win-crt-* (UCRT is inbox on Win10+).
- MSVC builds must use `/MT` (static CRT) so the DLLs never depend on vcruntime
  redists being installed system-wide.
