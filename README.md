# ApkShellext2_hy  
![](https://img.shields.io/badge/version-v3.3.0-blue.svg?style=flat)

A Windows shell extension supporting icon for files of
* .apk (android package)
* .ipa (iOS app package)
* .appx .appxbundle (Windows phone 8.1/10 app package, .xap is not supported)

#### [IMPORTANT]

**This project is based on `ApkShellExt2`, which has not been updated by the original author for two years, and I will continue to maintain this project if I have spare time. In addition, my other project is also based on this project, I hope you support more.**

#### [Change]

* [3.3.0] Localized app names & icon regressions
  * New: `android:label` localization — *Preferences ▸ Rename* gained "Use app name language" with a language dropdown (default: follow UI language). `{#appname}` in rename patterns and the info-tip now resolve the label's resource configurations with Android-like ranking: exact language+region > language without region > same language other region > locale-less default. Verified against a corpus of 83 APKs (18 with localized labels, e.g. Ice Box 冰箱, SnoreLab いびきラボ/鼾声分析器).
  * Fix: vector (adaptive/monochrome) icons collapsed to a solid color with SVG.NET 3.5.0 — `Convert2Path` now threads the current path point through `SvgPathSegment.AddToPath` instead of relying on the obsolete one-arg overload (segments' `Start` is no longer filled since 3.x).
  * Fix: PNG icons could paint blank — entry bytes are fully buffered before `Image.FromStream` (GDI+ defers PNG decoding, which deadlocks against SharpZipLib 1.4.2's non-seekable entry streams); applied to .apk, .aab and .appx logo paths.
  * Fix: the Preferences window was modal and froze the Explorer window until closed; it is now a modeless singleton.
  * Build: `System.Runtime.CompilerServices.Unsafe` facade raised to 6.0.0, resolving the MSB3277 conflict introduced by System.Text.Encoding.CodePages 5.0.0 (QRCoder PayloadGenerator).

* [3.2.0] Bug fixes (issue triage)
  * Fix: icon not shown when the archive stores its icon with an uppercase extension (`Icon.PNG`) — extension dispatch in `getImage` is now case-insensitive for .apk and .aab; `.APK`/`.IPA` infotip/context-menu detection likewise. (kkguo/apkshellext#99)
  * Fix: package file could not be deleted/renamed in Explorer while icon/infotip handlers were alive — all package FileStreams now opened with `FileShare.Delete`. (kkguo/apkshellext#103)
  * Fix: install.bat/uninstall.bat failed to elevate when invoked by a 32-bit process on Windows 10/11 (WOW64 runas bug); elevation now always runs through a 64-bit cscript and the 64-bit regasm is picked via PROCESSOR_ARCHITECTURE. (kkguo/apkshellext#116)
  * Fix: post-build batch silently failed on LF-only line endings (cmd GOTO limitation); `.gitattributes` now pins CRLF for `*.bat`/`*.cmd`.

* [3.1.0] Dependency modernization (still .NET Framework 4.8)
  * Security: native libwebp rebuilt from 1.0.3 (2013) to **1.6.0** — fixes CVE-2023-4863, CVE-2023-51294 and a dozen more; x86/x64 built with MSVC static CRT, libsharpyuv statically linked (`ApkShellext2/LibWebP/`, rebuild script in `build/libwebp/`).
  * WebP-wrapper updated to master: alpha-channel decode (transparent webp icons), x86 crash fix.
  * SharpZipLib: kkguo fork (0.86-era) → upstream **1.4.2** (fork's only private commit is already upstream). Better zip64 and entry-header lookup.
  * QRCoder 2018 snapshot → **1.4.2**; SVG.NET 2018 snapshot → **3.5.0** (+ ExCSS 4.2.3). Run `build/svg-pregen/pregen.ps1` once before first build.
  * Removed unmaintained Ionic.Zlib (PNGDecrush's zlib/CRC now uses SharpZipLib streams); removed dead Google.Protobuf references.
  * Build: strong-name now from a locally generated `apkshellext2_hy.snk` (`sn.exe -k`, not committed) since the original pfx was lost; Release packaging via `Resources/postbuild.cmd` finds 7-Zip automatically and only warns when absent.
  * Verified: VS MSBuild full-solution Debug/Rebuild 0 errors, signed main + 15 satellite assemblies, runtime smoke tests (webp lossy/alpha decode, zip, SVG path, QR, PNG zlib/CRC, DTD resource) pass.

* [3.0.0]
  * Fix: Crashes Chrome and Edge when downloading an apk/ipa/appx/ file.
  * Upgrade .Net Framework version to 4.8.0.

#### [Features]
 - [x] Display app icon in Windows File Explorer with best resolution. __DOES NOT work with other file manager due to .NET restriction__
 - [x] Customize-able Info-Tip for showing package information.
 - [x] Context menu for batch renaming, use customize-able patterns.
 - [x] Go to app store from context menu.
 - [x] Auto-check new version.
 - [x] Show overlay icon for different type of apps.
 - [x] Support multiple languages: Thanks to contributors on https://crowdin.com/project/apkshellext
   
#### [Todo]
 - [X] Adaptive-icon support (Beta)
 - [ ] protobuf support
 - [ ] support .NET 4.0??
 - [ ] QR code to download to phone
 - [x] Hook up adb function with namespace extension.
 - [x] drag-drop to install / uninstall to phone ([Please visit ApkInstaller]([1595901624/ApkInstaller: 一款可以在Windows上双击安装APK的软件 (github.com)](https://github.com/1595901624/ApkInstaller)))

#### [Donate]

* [PayPal](PayPal.Me/haoyu94)

* AliPay: 1595901624@qq.com

#### [Thanks]

Thank you to [kkguo](https://github.com/kkguo) for all open source projects.

|||
| --- | --- |
| [SharpShell](https://github.com/dwmkerr/sharpshell)                 | Shell extension library                        |
| [SharpZip](https://github.com/icsharpcode/SharpZipLib)              | Zip function implementation in C#              |
| [Iteedee.ApkReader](https://github.com/hylander0/Iteedee.ApkReader) | the original APK reader, not in use currently  |
| [PlistCS](https://github.com/animetrics/PlistCS)                    | iOS plist file reader                          |
| [PNGDecrush](https://github.com/MikeWeller/PNGDecrush)              | PNG decrush lib                                |
| [QRCoder](https://github.com/codebude/QRCoder)                      | QR code generator                              |
| [Ultimate Social](https://www.iconfinder.com/iconsets/ultimate-social) | A free icon set for social medias           |
| [SVG](https://github.com/vvvv/SVG)                                  | SVG format renderer                            |
| [WebP-Wrapper](https://github.com/JosePineiro/WebP-wrapper)         | WebP format renderer
| [Thanasis Georgiou](https://github.com/sakisds)                     | Project web page |
