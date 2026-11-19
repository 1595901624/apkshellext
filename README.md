# ApkShellext2_hy  
![](https://img.shields.io/badge/version-v3.4.0-blue.svg?style=flat)

A Windows shell extension supporting icon for files of
* .apk (android package)
* .apks .apkm .xapk (split-APK bundles, new in 3.4.0)
* .ipa (iOS app package)
* .appx .appxbundle (Windows phone 8.1/10 app package, .xap is not supported)

#### [IMPORTANT]

**This project is based on `ApkShellExt2`, which has not been updated by the original author for two years, and I will continue to maintain this project if I have spare time. In addition, my other project is also based on this project, I hope you support more.**

#### [Change]

* [3.4.0] Split-APK bundles & zip hardening
  * New: .apks / .apkm / .xapk split-bundle icons and info
  * Security: bounded, ratio-capped zip reads everywhere (anti zip-bomb / size overflow)
  * Security: XML drawable recursion depth & canvas size limits
  * 安全：为全部归档读取路径增设容量与解压比率上限，防御压缩炸弹、长度溢出与递归异常

* [3.3.1] Vector icons fixed at the root
  * Fix: facade assembly resolution inside explorer (vector icons silently blank)
  * Fix: vector `<group>` transforms honored; adaptive-icon fallback chain
  * 修复：宿主进程内依赖解析失败导致的矢量图标空白；支持矢量分组变换；完善自适应图标降级处理

* [3.3.0] Localized app names & icon regressions
  * New: localized `android:label` with language preference in Preferences
  * Fix: SVG 3.5.0 path regression; PNG blank-icon buffering; modeless Preferences
  * 新增：应用名称多语言支持；修复：SVG 与 PNG 图标渲染回归；首选项窗口改为非模态

* [3.2.0] Bug fixes (issue triage)
  * Fix: uppercase icon extensions (#99); FileShare.Delete (#103); WOW64 elevation (#116); CRLF build scripts
  * 修复：大写扩展名识别、文件占用冲突、64 位提权失败与构建脚本换行问题

* [3.1.0] Dependency modernization (still .NET Framework 4.8)
  * Security: libwebp 1.0.3 → 1.6.0 (CVE-2023-4863 etc.), SharpZipLib 1.4.2, QRCoder 1.4.2, SVG.NET 3.5.0, WebP-wrapper master
  * Removed Ionic.Zlib & dead Google.Protobuf refs; new signing key workflow; postbuild packaging fixes
  * 依赖库整体升级，修复 libwebp 相关安全漏洞

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
| [apk-info](https://github.com/delvinru/apk-info)                    | Split-APK (.apks/.apkm/.xapk) container layout, base-selection order & anti-zip-bomb doctrine |
| [apkspec](https://apkspec.readthedocs.io/)                          | Unofficial .apks / split-APK bundle specification |
