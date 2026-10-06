{{CHANGES}}

---

## 产物下载

点击文件名即可下载。

### 合并版（自包含，解压即用，无需预装运行时）

| 文件 | 类型 | 大小 | 说明 |
|---|---|---:|---|
| [`SeaMonkeys-{{VERSION}}-x64-merged-setup.exe`](https://github.com/iwvw/SeaMonkeys/releases/download/v{{VERSION}}/SeaMonkeys-{{VERSION}}-x64-merged-setup.exe) | x64 安装包 | {{SIZE_X64_MERGED_SETUP}} | 推荐。一键安装，自动创建桌面快捷方式 |
| [`SeaMonkeys-{{VERSION}}-x64-merged-portable.zip`](https://github.com/iwvw/SeaMonkeys/releases/download/v{{VERSION}}/SeaMonkeys-{{VERSION}}-x64-merged-portable.zip) | x64 便携版 | {{SIZE_X64_MERGED_PORTABLE}} | 解压即用，无需安装 |
| [`SeaMonkeys-{{VERSION}}-arm64-merged-portable.zip`](https://github.com/iwvw/SeaMonkeys/releases/download/v{{VERSION}}/SeaMonkeys-{{VERSION}}-arm64-merged-portable.zip) | ARM64 便携版 | {{SIZE_ARM64_MERGED_PORTABLE}} | ARM64 设备（如骁龙本） |

### 分离版（框架依赖，体积最小，需预装运行时）

> 需系统已安装 **.NET 10 桌面运行时** 与 **Windows App Runtime 2.x**，否则无法启动。仅提供 x64。

| 文件 | 类型 | 大小 | 说明 |
|---|---|---:|---|
| [`SeaMonkeys-{{VERSION}}-x64-split-setup.exe`](https://github.com/iwvw/SeaMonkeys/releases/download/v{{VERSION}}/SeaMonkeys-{{VERSION}}-x64-split-setup.exe) | x64 安装包 | {{SIZE_X64_SPLIT_SETUP}} | 一键安装；安装时会检测运行时并引导安装 |
| [`SeaMonkeys-{{VERSION}}-x64-split-portable.zip`](https://github.com/iwvw/SeaMonkeys/releases/download/v{{VERSION}}/SeaMonkeys-{{VERSION}}-x64-split-portable.zip) | x64 便携版 | {{SIZE_X64_SPLIT_PORTABLE}} | 解压即用，需自备运行时 |

### 选哪个？

| 场景 | 建议 |
|---|---|
| 大多数用户 | **合并版 x64 安装包**（自带运行时，装完即用） |
| 想体积最小、已装 .NET 10 与 Windows App Runtime | **分离版** |
| 不想安装、随处运行 | 合并版便携 zip |
| ARM64 设备（如骁龙本） | 合并版 ARM64 便携包 |

## 注意事项

- **首次运行**：应用会尝试自动检测《战舰世界》安装目录（支持 Steam 库）；未检测到请在设置页手动填写。
- **对局观察**：默认开启，游戏开局写入对局元数据后自动加载，无需手动操作。
- **回放渲染**：首次使用需下载渲染工具（约 24MB，支持加速前缀）。
- **分离版运行时**：需自行安装 .NET 10 桌面运行时与 Windows App Runtime 2.x，安装器会检测并引导。
- 用户数据位于 `%LocalAppData%\SeaMonkeys`，卸载时保留。
- 安装包未做代码签名，Windows SmartScreen 可能提示「未知发布者」，选择「仍要运行」即可。
