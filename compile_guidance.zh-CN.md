# ApkShellExt 编译指南(compile_guidance.zh-CN.md)

> 本文记录从**全新克隆**到产出可用 `ApkShellext2.dll`(含 19 个语言卫星程序集)与
> `apkshellextTest.exe` 的**完整、实际执行过**的流程。所有命令均于 2026-11-20 在
> `E:\src\apkshellext`(ApkShellExt **3.4.5**,HEAD `076f182`)实测通过,可直接复制,
> 只需调整盘符/路径。
>
> 本检出的两个工程文件已**现代化为 SDK 式 csproj**(§5),这是两条编译路线都能
> **零附加 MSBuild 参数**的原因。对照文档:[English compile_guidance.md](compile_guidance.md)。

## 0. 速览

| 项目 | 值 |
|---|---|
| 主产物 | `ApkShellext2\bin\Release\ApkShellext2.dll`(3.4.5.0,强名 `Pkt=fb82661d1c53098c`,本地密钥) |
| 卫星程序集 | `bin\Release\{ar-SA,…,zh-CN,zh-TW}\ApkShellext2.resources.dll` × 19 |
| 测试程序 | `apkshellextTest\bin\Release\apkshellextTest.exe` |
| 分发包 | `bin\Release\ApkShellext2.7z` + 19 × `<locale>.7z`(需 7-Zip;postbuild 自动打包) |
| 路线 A | 独立 .NET SDK **10.0.404**:`dotnet build ApkShellext2\ApkShellext2.csproj -c Release` —— 一行命令,零参数 |
| 路线 B | 装了*.NET 桌面开发*负载的 Visual Studio 2022/2026:`msbuild -t:restore` + `msbuild -p:Configuration=Release` —— 同样零参数 |
| 关键坑 | ① 签名密钥 `apkshellext2_hy.snk` 不在仓库里,要自己生成(§4.2);② 机器可能**一个 NuGet 源都没配**,restore 直接 NU1100(§4.1);③ dotnet CLI 编出的产物要额外带 `System.Resources.Extensions.dll`(其预序列化资源的运行时依赖,postbuild 已自动打进包,§8) |

## 1. 环境与两条编译路线

工程目标框架为 **.NET Framework 4.8**(`net48`),两个工程都已改为 **SDK 式 csproj**
(`<Project Sdk="Microsoft.NET.Sdk">`),`LangVersion=10.0`。因此任何足够新的 MSBuild
都能编译,**不依赖 Visual Studio**。

- **路线 A:** 独立 .NET SDK(≥ 8;在 10.0.404 上验证过),直接 `dotnet build`。
  无附加参数、无 shim、无预生成步骤。
- **路线 B:** Visual Studio(装「*.NET 桌面开发*」负载,自带 MSBuild + .NET SDK +
  Roslyn + NuGet)或 Build Tools。直接 `msbuild`,同样零附加参数。VS 2026(18)与
  VS 2022(17.x)皆适用。.NET Framework 4.8 目标包有帮助,但并非每台机器都严格
  必需(见 §4.4)。

两条路线产物功能一致;唯一可测差异(资源序列化模式 → 输出目录是否多一个
`System.Resources.Extensions.dll`)在 §8 说明。

下文示例路径的参考环境(按你的机器调整):Windows x64;仓库在
`E:\src\apkshellext`,工具链在 `E:\src\apkshellext-dep`;7-Zip 装在非标准路径
`D:\Software\7-Zip\`(注册表
`HKLM:\SOFTWARE\7-Zip` → `Path` 可查到,postbuild 会自动找到);v4.8 参考程序集位于
`C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8`。

## 2. 克隆仓库

默认直接从 GitHub 克隆:

```powershell
git clone https://github.com/nkh0472/apkshellext.git E:\src\apkshellext
```

只有 github.com 连不通或太慢时,才改用 githubfast 镜像(§3 的子模块同理):

```powershell
git clone https://githubfast.com/nkh0472/apkshellext.git E:\src\apkshellext
```

## 3. 子模块(5 个)

`.gitmodules` 里写的是 github.com,上面的克隆走直连时,直接:

```powershell
git -C E:\src\apkshellext submodule update --init --recursive --jobs 4
```

网络到不了 github.com 时,用一次性的镜像改写做同一件事(`-c` 让 `insteadOf` 只作用于
这一条命令,不落盘):

```powershell
git -C E:\src\apkshellext -c url."https://githubfast.com/".insteadOf="https://github.com/" submodule update --init --recursive --jobs 4
```

5 个子模块全部真实参与编译:

| 子模块 | 上游 | 说明 |
|---|---|---|
| `ApkShellext2\SharpZipLib` | icsharpcode/SharpZipLib @ `33f64eb` | glob 编译 `src\ICSharpCode.SharpZipLib\**`(排除 AssemblyInfo/GZip/Lzw/Tar/Core\ExactMemoryPool.cs) |
| `ApkShellext2\PlistCS` | kkguo/PlistCS @ `0fc69e7` | 源码级引用 |
| `ApkShellext2\PNGDecrush` | nkh0472/PNGDecrush @ `e926128` | 源码级引用 |
| `ApkShellext2\SVG` | vvvv-github/SVG **v3.5.0** @ `9d564f4` | 源码级引用;其**源生成器作为编译器 analyzer 自动运行**(§5),无手工步骤 |
| `ApkShellext2\WebP-wrapper` | JosePineiro/WebP-wrapper @ `79e4234` | 源码级引用(运行时还需 `LibWebP\libwebp_x86/x64.dll`,已提交在仓库,无需构建) |

注意:
- **SharpShell 不是子模块** —— 其源码(2.7.1)直接 vendored 在 `ApkShellext2\SharpShell\`,
  由 csproj 编译(死代码除外,§5.1)。
- 编译会把 SVG 子模块弄"脏"(`bin\`/`obj\`,analyzer 工程编译到此)。在子模块内做
  **本地**忽略,保持 `git status` 干净:把 `bin/`、`obj/` 追加到
  `E:\src\apkshellext\.git\modules\ApkShellext2\SVG\info\exclude`(精确路径用
  `git -C ApkShellext2\SVG rev-parse --absolute-git-dir` 查)。

## 4. 工具链与一次性准备

### 4.1 安装 .NET SDK(仅路线 A)——并确认 NuGet 有源

```powershell
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'E:\src\apkshellext-dep\dotnet-install.ps1' -UseBasicParsing
& 'E:\src\apkshellext-dep\dotnet-install.ps1' -Channel 10.0 -InstallDir 'E:\src\apkshellext-dep\dotnet-sdk-10' -NoPath
# 10.0 频道当前解析到 10.0.404(本文写作时)。任何 SDK >= 8 都行(analyzer 需要 Roslyn >= 4.8)。
```

**一次性检查 NuGet 源。** 用户级 `NuGet.Config` 里如果存在 `<packageSources>` 节但
内容为**空**,会静默禁用隐式的 nuget.org 默认源,restore 直接报
`error NU1100: 无法解析 net48 的 "System.Resources.Extensions (>= 6.0.0)"`(或第一个
PackageReference):

```powershell
& E:\src\apkshellext-dep\dotnet-sdk-10\dotnet.exe nuget list source   # 必须能看到 nuget.org
# 如果什么都没列出来:
& E:\src\apkshellext-dep\dotnet-sdk-10\dotnet.exe nuget add source https://api.nuget.org/v3/index.json -n nuget.org
```

两条路线都会被咬(VS 共用同一份用户配置)。旧式工程布局根本不需要 restore
(`packages\` 里的 HintPath DLL),所以没配源的机器以前从没暴露过这个问题 —— SDK 式
工程改为从 nuget.org restore ExCSS/System.Memory 等(§5.2)。

### 4.2 签名密钥 snk(本地生成 —— 生成方式不对会弄坏 VS 路线)

csproj 里 `SignAssembly=true`、`AssemblyOriginatorKeyFile=apkshellext2_hy.snk`。
官方私钥不随仓库发布,且 `*.snk` 被 gitignore。README 里写:*「Release builds sign
with `ApkShellext2\apkshellext2_hy.snk` — generate your own key with `sn -k`」*。
**自己生成一个就能编**(公钥与官方发布不同 —— 产物仅限本地使用,不可能冒充官方
签名版)。

`sn -k`(Windows SDK 工具)可以用,但其实不需要任何 SDK —— 系统自带的 Windows
PowerShell 5.1 就能产出同一种密钥,请用这个确切写法:

```powershell
& C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -NoProfile -Command "
  `$cp = New-Object Security.Cryptography.CspParameters
  `$cp.ProviderType = 1   # PROV_RSA_FULL
  `$cp.KeyNumber    = 2   # AT_SIGNATURE  <-- 关键就在这行
  `$rsa = New-Object Security.Cryptography.RSACryptoServiceProvider(`$cp)
  [IO.File]::WriteAllBytes('E:\src\apkshellext\ApkShellext2\apkshellext2_hy.snk', `$rsa.ExportCspBlob(`$true))"
# 596 字节;文件头应为 07 02 00 00 00 24 00 00 "RSA2"(aiKeyAlg=0x2400 CALG_RSA_SIGN)
```

**陷阱:** 想当然的写法
`New-Object System.Security.Cryptography.RSACryptoServiceProvider 1024` 默认生成
*密钥交换(key-exchange)*密钥(`aiKeyAlg=0xa400`)。Roslyn/csc 毫不挑剔(dotnet 路线
一路绿灯),但 Visual Studio 的 MSBuild 下给**卫星程序集**签名的经典 `al.exe` 会拒绝:
`ALINK : error AL1078 … 未知错误(8013141e)`。VS 路线见到 AL1078,先按上面的写法重新
生成 snk(坑 13),再考虑怀疑 VS 安装。

### 4.3 7-Zip(可选;只影响 postbuild 打包步骤)

`postbuild.cmd` 的查找顺序:`%ProgramFiles%\7-Zip` 与 `%ProgramFiles(x86)%\7-Zip` →
`HKLM:\SOFTWARE\7-Zip`(InstallPath、Path 两个值,**用 `reg /reg:64`、`/reg:32` 把
64 位和 32 位注册表视图都查一遍** —— 经典 `Bin\MSBuild.exe` 是 32 位,只查默认视图会
漏掉 64 位安装,坑 14)→ `where 7z`。找不到只打一条
**WARNING** 并跳过打包 —— DLL 产物不受影响。要出 `.7z` 包就装一个,例如:

```powershell
winget install 7zip.7zip
```

### 4.4 .NET Framework 4.8 参考程序集

所有 net48 编译都需要。当前 Windows 上一般已在
`C:\Program Files (x86)\Reference Assemblies\...\.NETFramework\v4.8`;也可通过 VS 组件
「.NET Framework 4.8 SDK/目标包」获得(*.NET 桌面开发*负载自带)。若都没有,部分 SDK
版本会自动回退到 `Microsoft.NETFramework.ReferenceAssemblies` NuGet 包 —— 别指望这个,
直接确认目录存在。

## 5. 本检出相对上游的改动(现代化)

上游是**老式** csproj。用 `dotnet msbuild` 编老式工程需要三个丑陋的绕行方案
(SVG 源生成器手工预生成、`MSB4064` 卫星程序集 shim、手工补 NuGet 包)。本检出把两个
csproj 重写为 **SDK 式**,从根上消掉这三件事 —— 这就是 §6–§7 所有命令零参数的原因。
全部改动以上游 3.4.5(HEAD `076f182`)为基线,按类别落成了若干提交 —— 构建体系迁移
(`ApkShellext2.sln`、两个 csproj、`Resources\postbuild.cmd`)、死代码清理、捐赠 UI
移除、拼写修复,以及本文档。本文写作时这些提交**只在本地,尚未 push** —— 先推送到你的
远端(或整体带走这个检出),否则别处的全新克隆并不能直接用下面这些零参数命令编译。

### 5.1 老绕行方案 → SDK 式替代

| 上游/老式做法 | 现在 |
|---|---|
| SVG v3.5.0 元素元数据要编译前手工生成(`pregen.ps1` → `SVG\Source\Generated\*.g.cs`) | `<ProjectReference Include="SVG\Generators\Svg.Generators.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />` —— 同一个上游生成器**在 Roslyn 内**每次编译自动跑,两条工具链都行 |
| coreclr MSBuild 下卫星程序集崩(`MSB4064` AL/ToolPath)→ 外部 `dotnet-satellites.targets` shim + 2 个命令行参数 | SDK 式工程在 coreclr 下自动 `GenerateSatelliteAssembliesForCore=true`(用 Csc 生成);FullFramework MSBuild 用真 `al.exe`。什么都不用传 |
| 手工下载 `packages\System.Runtime.CompilerServices.Unsafe.6.0.0`(HintPath 工程,无 restore) | `PackageReference` 全量 restore:ExCSS 4.2.3、System.Memory 4.5.5(带出 Buffers + Numerics.Vectors)、System.Runtime.CompilerServices.Unsafe 6.0.0(直接引用压过传递依赖的 4.5.3,与老 HintPath 解析结果一致),以及**仅在 coreclr 下**启用的 System.Resources.Extensions 6.0.0 |
| 用户手敲 `-p:GenerateResourceUsePreserializedResources=true` | 写进 csproj:`Condition="'$(MSBuildRuntimeType)' == 'Core'"` —— dotnet CLI 自动用预序列化资源,VS 用经典资源,自动分岔(唯一运行时后果见 §8) |
| .sln 里 `apkShellextService` 死条目(目录从来不存在) | 已从 .sln 删除 |
| csproj `Content` 引用上游在 `4f30b7c` 删掉的 `SharpShellNativeBridge32/64.dll`;引用它们的 SharpPropertySheet 属性页机制是死代码 | 相应 `Content` 项已删除;死代码目录 `NativeBridge\`、`SharpPropertySheet\`、`SharpDeskBand\` 已从仓库删除;`Helpers\RegAsm.cs` 与 vendored `Properties\AssemblyInfo.cs` 仍在盘上,但它们从来不是编译输入。没有任何被编译的文件引用这些命名空间 |

### 5.2 其他现代化说明

- 老的 `packages\` 目录现在**不再被使用**(残留,无害)。不要再加回 HintPath 引用。
- `AppendTargetFrameworkToOutputPath=false` 保持扁平 `bin\Release` 布局 —— 安装脚本、
  `postbuild.cmd` 和本指南都按这个布局说话(SDK 默认是 `bin\Release\net48\`)。
- `GenerateAssemblyInfo=false`:`Properties\AssemblyInfo.cs` 仍是版本/Guid/ComVisible
  特性的唯一来源。
- `EnableDefaultCompileItems/EmbeddedResourceItems/NoneItems=false` + 显式 glob ——
  vendored 子模块树里有大量**不该**被编译的文件(它们自己的 AssemblyInfo、示例、
  工具、WinUI/WPF 变体)。
- 保留了老的 `Debug2` 配置(`Configurations=Debug;Debug2;Release`),行为等同 Debug。
  生成器 ProjectReference 通过 `SetConfiguration` 把它映射到生成器自身的 Debug 输出。

## 6. 编译主工程

### 路线 A —— dotnet CLI

```powershell
Set-Location E:\src\apkshellext
E:\src\apkshellext-dep\dotnet-sdk-10\dotnet.exe build ApkShellext2\ApkShellext2.csproj -c Release --nologo
```

就一行。restore 隐式执行。预期:`EXIT=0`,只有七条代码里原有的警告
(CS0618 过时 SVG API ×2、CS0219 未用变量 ×4、CS0169 未用字段),输出
`ApkShellext2 -> …\bin\Release\ApkShellext2.dll`,随后是 7z 打包步骤(§8)。

### 路线 B —— Visual Studio / Build Tools 的 MSBuild

```powershell
# 定位 MSBuild(知道路径就跳过):
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msb = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1

Set-Location E:\src\apkshellext
& $msb ApkShellext2\ApkShellext2.csproj -t:restore -p:Configuration=Release -nologo -v:m
& $msb ApkShellext2\ApkShellext2.csproj -p:Configuration=Release -nologo -v:m
```

两条命令(经典 MSBuild 的 restore **不**隐式),依然零自定义参数。注意冒号:经典
MSBuild 要 `-v:m` 而不是 `-v m`(坑 3)。restore/编译若报
`MSB4236 找不到指定的 SDK“Microsoft.NET.Sdk”`,说明 VS 安装坏了 —— 坑 2。
`Bin\MSBuild.exe` 是 **32 位**启动器(64 位在 `Bin\amd64\`);在这里两者都能用 ——
postbuild 的 7-Zip 定位就是专门做成位宽无关的(坑 14)。VS 18 还在
`C:\Program Files\dotnet\sdk\` 自带一份 SDK,路线 B 完全不依赖路线 A 装的东西。

整个解决方案(主工程+测试)两条路线也都能一把梭:
`dotnet build ApkShellext2.sln -c Release` / `msbuild ApkShellext2.sln -t:restore` +
`msbuild ApkShellext2.sln -p:Configuration=Release`。

## 7. 编译测试工程

```powershell
# 路线 A:
E:\src\apkshellext-dep\dotnet-sdk-10\dotnet.exe build apkshellextTest\apkshellextTest.csproj -c Release --nologo
# 路线 B:同样的两条 msbuild 命令,工程换成 apkshellextTest\apkshellextTest.csproj
```

它是个 WinForms 程序,引用主工程(外加一个 link 文件 `..\ApkShellext2\ApkChunk.cs`),
不签名,无 NuGet 依赖。Debug 编译同理(`-c Debug` / `-p:Configuration=Debug`);Debug
输出会额外多一个 `debug.bat`(仅 Debug 复制的开发辅助脚本,§8)。别再找
`apkShellextService` —— 它的 .sln 死条目已在本检出删除,磁盘上从来就没有过。

## 8. 发布布局、postbuild 与部署说明

**布局由 MSBuild 组装 —— postbuild 只负责打包。**

- **MSBuild 侧**(`ApkShellext2.csproj`):`install.bat`、`install_user.bat`、
  `uninstall.bat`、`uninstall_user.bat`、`restart_explorer.bat` 和两个 `libwebp_*.dll`
  都是 `CopyToOutputDirectory=PreserveNewest` 的 `Content` 项;`debug.bat` 同理但带
  `Condition="'$(Configuration)'=='Debug'"`。`CopyFilesToOutputDirectory` 在任何打包
  之前就把它们复制到位,所以 `bin\Release` 本身就是完整发布目录。每个项都带
  `TargetPath=<裸文件名>` —— 不带的话复制会在输出里重建 `Resources\`/`LibWebP\` 源
  子目录(坑 5)。
- **打包钩子**:实现为 `<Target Name="PackageWith7z" AfterTargets="PostBuildEvent">`
  用 `Exec` 调脚本,**不是**老的 `<PostBuildEvent>` 属性 —— SDK 式工程里属性赋值里的
  宏在**求值期**展开,那时 `$(ProjectDir)`/`$(TargetDir)` 还没有值,老一行式会被烤成
  `call "Resources\postbuild.cmd" "" ""`(坑 4)。
- **`Resources\postbuild.cmd`**(仅 Release 干活;Debug 直接退出):删残留 `debug.bat`,
  定位 `7z.exe`(§4.3),删残留 `*.7z`,然后打 `ApkShellext2.7z` = `ApkShellext2.dll` +
  `*.bat` + `libwebp_*.dll` + `ExCSS.dll` + `System.Memory.dll` + `System.Buffers.dll` +
  `System.Runtime.CompilerServices.Unsafe.dll`,**外加(若存在)`System.Resources.Extensions.dll`**
  —— 见下 —— 再为每个语言目录各打一个 `<locale>.7z`。没有 7-Zip ⇒ WARNING + 跳过,
  编译照样成功。

**两条路线唯一差异 —— `System.Resources.Extensions.dll`。** coreclr MSBuild(dotnet CLI)
下 csproj 启用预序列化资源生成;生成的 `.resources` blob 内嵌该程序集的类型,所以
**dotnet CLI 编出的产物运行时需要 `ApkShellext2.dll` 旁边放着
`System.Resources.Extensions.dll`**(实测:不放它,第一次触碰非字符串资源 —— 比如读
带图标的 `NonLocalizeResources` —— 就抛
`FileNotFoundException: System.Resources.Extensions, Version=4.0.0.0, Pkt=cc7b13ffcd2ddd51`;
而 `btnOK` 这类字符串取值依然正常,所以此坑很容易被漏掉)。Visual Studio(FullFramework)
编译走经典资源序列化,根本不引用该包,也不产生这个文件 —— 与上游发布完全一致。
`postbuild.cmd` 自动适配(文件在就打进去),所以各路线的 `.7z` 自洽:dotnet 路线
**13 个文件**,VS 路线 **12 个**。不要手动把 `System.Resources.Extensions.dll` 塞进
VS 路线的输出目录 —— 那里用不到。

部署(目标机,以要用这个外壳扩展的用户身份):解压 `ApkShellext2.7z`(+需要的
`<locale>.7z`,让语言目录和 DLL 同级),跑 `install.bat`(管理员,全机)或
`install_user.bat`(按用户;两者都 regasm 注册并 NGEN 预编译 —— 3.4.5 修好了脚本里的
NGEN)。`restart_explorer.bat` 重载外壳。卸载用对应的 `uninstall*.bat`。本地 snk 与
上游不同,别把本地编译的 DLL 和官方包混在同一注册/GAC 状态里。

## 9. 编译结果验证

1. **卫星目录:** `bin\Release` 必须有全部 19 个语言目录
   (`ar-SA de-DE el-GR es-ES fa-IR fr-FR he-IL it-IT ja-JP ko-KR my-MM nl-NL pl-PL pt-PT
   ru-RU tr-TR uk-UA zh-CN zh-TW`),各含 `ApkShellext2.resources.dll`。
2. **反射验身份** —— 必须用 **Windows PowerShell 5.1**
   (`C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`);PowerShell 7(.NET Core)
   无法正确反射这些 .NET Framework 程序集。公钥令牌字节要用 `{0:x2}` 格式化 ——
   直接对字节数组 `-join` 得到的是十进制数字串,不是十六进制令牌:

   ```powershell
   $ma = [Reflection.Assembly]::LoadFrom('E:\src\apkshellext\ApkShellext2\bin\Release\ApkShellext2.dll')
   $ma.GetName() | Format-List Name, Version   # ApkShellext2  3.4.5.0
   (($ma.GetName().GetPublicKeyToken() | % { '{0:x2}' -f $_ }) -join '')   # fb82661d1c53098c  (本地密钥)
   $sat = [Reflection.Assembly]::LoadFrom('E:\src\apkshellext\ApkShellext2\bin\Release\zh-CN\ApkShellext2.resources.dll')
   $sat.GetName() | Format-List Version, CultureName   # 3.4.5.0, zh-CN — Pkt 与主 DLL 相同
   ```

3. **ResourceManager 往返**(证明卫星可被检索,而不只是存在):

   ```powershell
   $rm = New-Object System.Resources.ResourceManager('ApkShellext2.Properties.Resources', $ma)
   $rm.GetString('btnOK', [cultureinfo]'zh-CN')              # → 确定   (来自卫星)
   $rm.GetString('btnOK', [cultureinfo]::InvariantCulture)   # → OK     (中性资源,在主 DLL 内)
   ```

4. **`bin\Release` 完整发布布局:** `ApkShellext2.dll(.pdb)`、五个 bat、
   `libwebp_x86/x64.dll`、copy-local 的 NuGet DLL(`ExCSS`、`System.Memory`、
   `System.Buffers`、`System.Numerics.Vectors`、`System.Runtime.CompilerServices.Unsafe`;
   `System.Resources.Extensions` 仅 dotnet 路线有),以及 —— 找到 7-Zip 时 ——
   `ApkShellext2.7z`(约 959 KB)+ 19 × `<locale>.7z`(各约 5 KB)。`7z l` 必须在包根
   **恰好列出 13 个文件(dotnet 路线)或 12 个(VS 路线)**,且 `bin\Release` 里不得有
   `Resources\`/`LibWebP\` 子目录。
5. **Debug 布局:** `bin\Debug` 根有 `debug.bat` + bat + libwebp,且**没有 `.7z`**。

## 10. 坑清单

1. **restore 报 NU1100** → 机器**一个 NuGet 源都没有**(用户 `NuGet.Config` 里的空
   `<packageSources>` 节会干掉隐式默认源)。`dotnet nuget list source` 必须列出
   nuget.org;按 §4.1 添加。
2. **VS 的 MSBuild 报 `MSB4236 / MSB4276: 无法解析 SDK“Microsoft.NET.Sdk”`**(常伴随
   `vswhere` 静默无输出)= VS 安装损坏/不完整 —— `MSBuild\Sdks` 是空的。正解:
   VS Installer → **修复**,或带「*.NET 桌面开发*」负载重装。修复或干净重装后,
   vswhere 能解析,restore/build 零额外参数即可跑通。
3. **经典 MSBuild 开关带冒号**:`-v:m`、`-p:Configuration=Release`;`-v m`(空格形式,
   `dotnet build` 可用)会被 `MSBuild.exe` 拒收。
4. **不要把打包钩子改回 `<PostBuildEvent>` 属性** —— SDK 式工程里该属性的宏在**求值期**
   展开(那时 `$(TargetDir)` 等还是空),脚本会静默收到 `"" ""`。保持
   `AfterBuild` 式的 `Exec` target。
5. **`Content` + `CopyToOutputDirectory` 会在输出里重建源子目录**,除非每项都写
   `TargetPath=<裸文件名>`;并且 7z 步骤的 `*.bat` 通配符对**零匹配静默忽略** ——
   包照样生成但里面没有 bat,也不报错。动过布局项就查 `7z l` 和输出树。
6. **路线相关的运行时文件**:dotnet CLI 编出的产物运行时需要
   `System.Resources.Extensions.dll`(§8)。postbuild 自动打包;别从 dotnet 路线的包里
   删它,也别往 VS 路线的部署里加它。
7. **snk**:被 gitignore;本地生成(§4.2)。公钥 ≠ 官方构建。
8. **老残留是有意的**:`packages\` 目录现已不再使用;NativeBridge 原生 DLL 在上游
   `4f30b7c` 里就是有意删除的,引用它们的死代码已从仓库删除(§5.1)。两者都不要"修"。
9. **编译会把 SVG 子模块弄脏**(`bin\`/`obj\`)→ 加进子模块本地 `info/exclude`
   (§3 注意),否则 `git status` 会骗人。
10. **MSBuild node-reuse 工作进程活着时改名/移动 SDK 目录会「Access denied」**
    (`MSBuild.exe … /nodemode:2 /nodereuse:True`,从 SDK 目录启动;
    `dotnet build-server shutdown` 不一定停得掉)。用
    `Get-CimInstance Win32_Process | ? CommandLine -match '<sdk-root>'` 找到持有者先停掉。
11. **PATH 上的 `dotnet` 可能只有运行时。** 用 `dotnet --version` 验证;报无 SDK 就用
    已装 SDK 的完整路径(本文所有命令都这么写)。
12. **四个文件是有意不跟随上游的**(§5):`.sln`、两个 csproj、`postbuild.cmd`。
    把任何一个回退成上游版本,就等于把老绕行方案(参数、shim、pregen、手工补包)
    请回来。
13. **VS 路线报 `ALINK : error AL1078 对程序集签名时出错 - 未知错误(8013141e)`** =
    snk 是*密钥交换(key-exchange)*blob(`aiKeyAlg=0xa400`,裸
    `RSACryptoServiceProvider 1024` 的默认产物)。csc 无视该字段,`al.exe` 拒收。
    用 `sn -k` 或 §4.2 的 PowerShell 片段(`KeyNumber=Signature`,头 `… 00 24 00 00
    "RSA2"`)重新生成。不是 VS 安装问题,也不是仓库问题。
14. **VS 命令行的 `Bin\MSBuild.exe` 是 32 位**,它的 Exec 子进程看到的是 WOW64 重定向后
    的注册表(`HKLM\SOFTWARE\7-Zip`"不存在")和指向 `Program Files (x86)` 的
    `%ProgramFiles%` —— postbuild 里裸的 `reg query` 会静默跳过打包而编译照样绿。
    `postbuild.cmd` 因此显式查两个视图(`/reg:64`、`/reg:32`);改脚本时保留这两个开关。
    (VS IDE 本身用 64 位引擎构建,所以这个坑只咬 x86 启动器的命令行构建。)

## 附录 A —— 一键参考脚本(路线 A)

```powershell
$R = 'E:\src\apkshellext'
$DOTNET = 'E:\src\apkshellext-dep\dotnet-sdk-10\dotnet.exe'

# --- 0. 前置(一次性):子模块 §3、NuGet 源 §4.1、snk §4.2 ---
& $DOTNET nuget list source | Select-String nuget.org -Quiet | % { if (-not $_) { throw '没有 nuget.org 源 - 见 §4.1' } }
if (-not (Test-Path "$R\ApkShellext2\apkshellext2_hy.snk")) { throw '先生成 snk - 见 §4.2' }

# --- 1. 主工程 + 测试(restore 隐式;analyzer 生成器在编译内自动跑) ---
Set-Location $R
& $DOTNET build ApkShellext2\ApkShellext2.csproj     -c Release --nologo; if ($LASTEXITCODE) { throw '主工程编译失败' }
& $DOTNET build apkshellextTest\apkshellextTest.csproj -c Release --nologo; if ($LASTEXITCODE) { throw '测试工程编译失败' }

# --- 2. 验证(必须在 Windows PowerShell 5.1 下跑,见 §9) ---
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

## 附录 B —— 一键参考脚本(路线 B)

```powershell
$R = 'E:\src\apkshellext'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msb = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msb) { throw '找不到 MSBuild - 修复 VS / 安装 .NET 桌面开发负载(§10 坑 2)' }

Set-Location $R
& $msb ApkShellext2.sln -t:restore            -p:Configuration=Release -nologo -v:m; if ($LASTEXITCODE) { throw 'restore 失败' }
& $msb ApkShellext2.sln -p:Configuration=Release              -nologo -v:m; if ($LASTEXITCODE) { throw '编译失败' }

# 验证 VS 路线特有项:输出里没有 System.Resources.Extensions.dll,包为 12 文件
Test-Path "$R\ApkShellext2\bin\Release\System.Resources.Extensions.dll"   # 应为 False
& 'D:\Software\7-Zip\7z.exe' l "$R\ApkShellext2\bin\Release\ApkShellext2.7z"   # 应为 12 个文件(7z 路径按机器调整)
```

---

*对照文档:[English version compile_guidance.md](compile_guidance.md)。*