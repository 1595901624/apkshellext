# ApkShellext2

[![release](https://img.shields.io/badge/release_3.5.0-blue)](https://github.com/nkh0472/apkshellext/releases)
[![.NET](https://img.shields.io/badge/.NET_Framework-4.8-512BD4)](#build-from-source)
[![license](https://img.shields.io/badge/license-MIT-green)](LICENSE)

**English** | [简体中文](README.zh-CN.md)

A Windows shell extension that shows the real app icon, package info tip and a management context menu for mobile app packages in File Explorer.

在资源管理器中为手机应用安装包显示真实图标、悬浮信息与右键管理菜单的 Windows 外壳扩展。支持 Windows 10 / 11，运行于 .NET Framework 4.8。

## Supported file types

| Extension | Icon | Info tip | Context menu | Thumbnail |
|-----------|:----:|:--------:|:------------:|:---------:|
| `.apk`    | ✔ | ✔ | ✔ | ✔ |
| `.apks` `.apkm` `.xapk` (split bundles, since 3.4.0) | ✔ | ✔ | ✔ | — |
| `.aab`    | ✔ | — | — | — |
| `.ipa`    | ✔ | ✔ | ✔ | — |
| `.appx` `.appxbundle` | ✔ | ✔ | ✔ | — |

`.xap` is not supported.

## Features

- Real package icons at every Explorer zoom level, with Android adaptive-icon rendering and automatic fallback to the legacy icon when a variant cannot be drawn
- Modern rendering pipeline: VectorDrawable → SVG rasterization (linear/radial gradients, nested groups, clip paths), adaptive icons with Android's 1.5x center-crop zoom, framework color references — pixel-consistent with Android's own output
- Localized app names: labels follow your UI language, or a language you pick in Preferences (since 3.3.0)
- Info tip with version, package name, size, target platform and other parsed metadata
- Context menu: batch rename with custom patterns, open the app's store page
- Overlay badges distinguishing app types
- Localized UI in 19 languages — translation pull requests welcome
- Hardened archive parsing: decompression-ratio and size caps on every read path (since 3.4.0), libwebp 1.6.0 (since 3.1.0)

## Install

1. Download the latest release archive and extract it to a permanent folder (e.g. `C:\Program Files\ApkShellext2\`).
2. Run `install.bat` **as administrator** — it registers the extension for both 64-bit and 32-bit Explorer hosts and generates NGEN native images (faster first icons after boot). No admin rights? Run `install_user.bat` instead: per-user registration under `HKCU`, no UAC prompt (remove again with `uninstall_user.bat`); it skips NGEN by design, so only the very first icons after each boot JIT-compile slightly slower. A machine-wide registration always takes precedence over the per-user one. Note: `install.bat` refuses folders that ordinary users can write to (that would let any local account replace the DLL every other user's Explorer loads) — move the extracted folder to `%ProgramFiles%`, or just use `install_user.bat`.
3. If icons do not refresh, run `restart_explorer.bat`.

**Uninstall:** run `uninstall.bat` as administrator — it unregisters both 64-bit and 32-bit views, sweeps the registration even if the DLL was already deleted (safe to run from any install folder of this version), and removes the legacy COM entries left by pre-3.4.2 builds. If file icons persist afterwards, the leftover is the Windows icon cache (`IconCache.db` / `thumbcache_*.db`), not the extension; delete it and restart Explorer.

## Settings

Right-click a package → **ApkShellext2 → Preferences** (modeless, never blocks Explorer). Settings live under `HKCU\SOFTWARE\ApkShellext2`, including `SupportAdaptiveIcon`, `UseAppNameLanguage` / `AppNameLanguage`, `CheckForUpdates` (set to `False` to stop the daily background version check) and info-tip customization.

## Build from source

The full build & deploy guide — toolchain choice (VS vs dotnet CLI and why only one produces deployable binaries), packaging, libwebp rebuild, common pitfalls — is [compile_guidance.md](compile_guidance.md) ([简体中文](compile_guidance.zh-CN.md)).

Quick version: clone with `--recursive`, then `msbuild ApkShellext2.sln /p:Configuration=Release /p:Platform="Any CPU"`. Requires Visual Studio 2019+ and the .NET Framework 4.8 targeting pack; release builds sign with your own `sn -k` key.

## Project history & maintainers

| Period | Maintainer | Contribution |
|--------|-----------|--------------|
| 2009 – | [kkguo](https://github.com/kkguo) | Original author of ApkShellext2 |
| – 2023 | [1595901624](https://github.com/1595901624) | Maintained v3.x, created the `ApkShellext2_hy` branch (.aab support and more) |
| 2026 – | [nkh0472](https://github.com/nkh0472) | Current maintainer: dependency modernization (libwebp 1.6.0), Windows 11 icon fixes, localized labels, split-APK bundles, archive hardening |

## Changelog

Full bilingual release notes are on the [Releases page](https://github.com/nkh0472/apkshellext/releases).

| Version | Summary |
|---------|---------|
| 3.5.0 | Adaptive icons out of beta: Android-exact 1.5x zoom, framework color references, case-exact zip entries, DPI-independent vector rendering; `<bitmap>` support |
| 3.4.5 | Fixed the scripts' NGEN steps (`/quiet` is not an ngen.exe flag — native images were never being generated) |
| 3.4.4 | Security/perf audit hardening; all 19 locales completed; AssemblyVersion finally tracks the release |
| 3.4.3 | NGEN native images; optional per-user (no-admin) install; dark-theme Preferences; update check opt-out |
| 3.4.2 | Modernized install/uninstall scripts: dual-bitness registration, offline registry sweep, legacy COM-entry cleanup; COM surface restricted to the four shell handlers |
| 3.4.1 | VectorDrawable rendering re-based onto the embedded SVG engine (gradients incl. multi-stop/angle/radial, nested groups, clip paths); indirect color references resolved |
| 3.4.0 | Split-APK bundles (`.apks`/`.apkm`/`.xapk`); zip-bomb & overflow hardening in all readers; MSIX bundle member-name decoding |
| 3.3.1 | FacadeResolver — fixes silent vector-icon failure inside Explorer; vector group transforms; adaptive-icon fallback chain |
| 3.3.0 | Localized app names; SVG/PNG icon regression fixes; modeless Preferences |
| 3.2.0 | Case-insensitive icon extensions (#99); FileShare.Delete (#103); WOW64-safe elevation (#116) |
| 3.1.0 | Dependency modernization: libwebp 1.6.0 (CVE-2023-4863 et al.), SharpZipLib 1.4.2, QRCoder 1.4.2, SVG.NET 3.5.0 — *had an SVG regression, fixed in 3.3.1* |
| 3.0.0 | Fixed Chrome/Edge download crash; upgraded to .NET Framework 4.8 |

## Thanks

The project stands on these open-source works:

| Component | Role |
|-----------|------|
| [SharpShell](https://github.com/dwmkerr/sharpshell) | Shell extension framework |
| [libwebp](https://chromium.googlesource.com/webm/libwebp) | WebP codec (1.6.0, built from source) |
| [WebP-wrapper](https://github.com/JosePineiro/WebP-wrapper) | WebP C# bindings |
| [SharpZipLib](https://github.com/icsharpcode/SharpZipLib) | Zip archive access |
| [SVG](https://github.com/vvvv/SVG) | SVG / vector-drawable rendering |
| [PlistCS](https://github.com/animetrics/PlistCS) | iOS plist reader |
| [PNGDecrush](https://github.com/MikeWeller/PNGDecrush) | PNG zlib re-encoding |
| [apk-info](https://github.com/delvinru/apk-info) | Split-APK container layout & anti-zip-bomb doctrine |
| [apkspec](https://apkspec.readthedocs.io/) | Unofficial split-APK bundle specification |
| [Ultimate Social](https://www.iconfinder.com/iconsets/ultimate-social) | Overlay icon set |

## License

[MIT](LICENSE) — Copyright (c) 2015 Jingang Guo, KK; Copyright (c) 2023 1595901624; Copyright (c) 2026 nkh0472.
