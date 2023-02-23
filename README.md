# ApkShellext2_hy  
![](https://img.shields.io/badge/version-v3.0.0-blue.svg?style=flat)

A Windows shell extension supporting icon for files of
* .apk (android package)
* .ipa (iOS app package)
* .appx .appxbundle (Windows phone 8.1/10 app package, .xap is not supported)

#### [IMPORTANT]

**This project is based on `ApkShellExt2`, which has not been updated by the original author for two years, and I will continue to maintain this project if I have spare time. In addition, my other project is also based on this project, I hope you support more.**

#### [Change]

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
| [Ionic.Zlib](https://github.com/jstedfast/Ionic.Zlib)               | Another Zip implementation, used by PNGDecrush |
| [QRCoder](https://github.com/codebude/QRCoder)                      | QR code generator                              |
| [Ultimate Social](https://www.iconfinder.com/iconsets/ultimate-social) | A free icon set for social medias           |
| [SVG](https://github.com/vvvv/SVG)                                  | SVG format renderer                            |
| [WebP-Wrapper](https://github.com/JosePineiro/WebP-wrapper)         | WebP format renderer
| [Thanasis Georgiou](https://github.com/sakisds)                     | Project web page |
