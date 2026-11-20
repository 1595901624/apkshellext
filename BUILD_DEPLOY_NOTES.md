ApkShellext2 构建与部署要点（务必遵守）:

1. **发布构建只用 VS MSBuild (Route B)**:
   `MSBuild.exe ApkShellext2\ApkShellext2.csproj -t:restore -p:Configuration=Release -p:OutDir=bin\VSRelease\` 然后同参数去掉 -t:restore 构建。
   输出在 bin\VSRelease，.resources 用经典 BinaryFormatter 序列化，宿主 (explorer/dllhost, CLR4) 无需 System.Resources.Extensions。
   **禁止用 dotnet CLI 构建产物部署**：coreclr GenerateResource 用 PreserializedResourceWriter (magic 0xCECAEBEF) 写 .resources，运行时反序列化需要 System.Resources.Extensions.dll；且 VS 构建会把 CLI 构建标记为 GenerateResourceUsePreserializedResources 的产物当成经典格式误读 → CreateMenu 抛 EndOfStreamStreamException/FileNotFoundException → 右键菜单消失但图标正常。

2. **安装目录 (D:\Software\ApkShellext2) 绝不放 System.Resources.Extensions.dll**（任何版本）。

3. **部署被锁的 dll**: 先停 explorer+dllhost；若仍被占用，用 stale pwsh (Add-Type) 排查 → Stop-Process；仍锁定时 `Rename-Item dll dll.old` 一定成功（explorer 以非 delete 共享打开），再 Copy-Item 新文件进去，删 .old。
   explorer 重启一律用预建计划任务 `apkdbg-re`（schtasks /run /tn apkdbg-re），禁止从会话直接 Start-Process explorer（会产生无桌面的浏览器态实例）。

4. 部署后必清 %LOCALAPPDATA%\Microsoft\Windows\Explorer 下 thumbcache_*.db / iconcache_*.db（explorer 停止后删）。
