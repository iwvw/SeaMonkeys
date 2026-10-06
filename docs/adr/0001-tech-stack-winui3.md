# ADR-0001. 技术栈：WinUI 3 + Windows App SDK + .NET 10

日期：2026-10-04
状态：Accepted

## 背景

需要为《战舰世界》做一个 Windows 桌面实时战绩助手。核心约束：

1. 游戏只有 Windows 客户端，无需跨平台。
2. 已存在两个可参考/复用的成熟自研 WinUI 3 项目：`E:\Code\sox`（文件搜索）与 `E:\Code\momomi`（代理客户端），二者共用 `.NET 10 + WinUI 3 + Windows App SDK` 栈，且已有验证过的组件与发布流水线。
3. 社区同类实现海猴雷达 ApeRadar 为 C# WPF，其领域逻辑（对局解析、多战绩源适配、加权胜率、更新）可借鉴，但 UI 栈较旧。

## 决策

**采用 C# + .NET 10 LTS + WinUI 3 + Windows App SDK 2.x**，与 sox / momomi 技术栈对齐。

分层：

```
SeaMonkeys.App     WinUI 3 主程序：悬浮面板、主窗、托盘、设置、图表
SeaMonkeys.Core    零 UI 依赖：对局解析、服务器识别、战绩源适配、加权胜率、舰船目录
SeaMonkeys.Data    SQLite 数据层：设置、观察名单、对局历史
```

复用清单（来源项目）：

| 能力 | 来源 |
|---|---|
| 无边框置顶小窗 + 滑入滑出 + DPI + 位置记忆 | momomi `Mini/` |
| 窗口常驻 + DWM cloak | sox `Interop/WindowCloak.cs` |
| 全屏检测 | sox `Services/FullscreenDetector.cs` |
| 卡片材质（Acrylic/Mica + ThemeShadow） | sox `Controls/CardControl.xaml.cs`、`Materials/` |
| 极简折线/分布图 | momomi `Controls/SparklineChart.cs` |
| SQLite（WAL + 读写锁 + Repository） | momomi `Data/` |
| 托盘（H.NotifyIcon.WinUI）、全局快捷键、主题、自更新 | sox / momomi 同名服务 |
| 发布流水线（双形态、语言裁剪、运行时精简） | sox / momomi `.github/workflows/release.yml` |
| 版本集中维护 | 两项目 `Directory.Build.props` |

## 备选与未选原因

- **WPF**：ApeRadar 的选择，可充分利用其源码，但 UI 栈旧，且无法直接复用 sox/momomi 的卡片材质与 cloak 架构。
- **Avalonia / Electron / Tauri / Rust 原生**：跨平台或体积/性能收益对本项目无实际意义，且无法复用现有资产。

## 后果

- 直接复用两个已验证的 WinUI 3 项目，M1 可跳过大量 UI 基建。
- 需注意 WinUI 3 关闭 trimming（XAML 反射依赖）、冷启动较慢（由常驻 + cloak 抵消）。
- ApeRadar 的 WPF UI 不移植，只借鉴其 `Core` 领域逻辑。

## 关联

- 依据实现资产：`E:\Code\sox`、`E:\Code\momomi`
- 相关：ADR-0002（数据来源）、ADR-0006（显示形态）
