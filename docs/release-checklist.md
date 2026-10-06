# 发布检查清单

每次发布前逐项确认。带「必须」的项不可跳过。

## 1. 版本与元数据（必须）

- [ ] 更新 `Directory.Build.props` 的 `SeaMonkeysVersion`。
- [ ] 确认版本号在标题栏、关于页显示正确。

## 2. 船名表刷新（必须）

船名表由本地游戏数据离线生成，官方译名且覆盖最全。发布前必须刷新，避免新船显示为未知。

- [ ] 确保本机已安装目标版本游戏，且游戏已更新到最新。
- [ ] 运行：
      ```
      pwsh -NoProfile -ExecutionPolicy Bypass -File build/update-ships.ps1 -GamePath "<World of Warships 目录>"
      ```
      可用环境变量替代：`WOWS_GAME_PATH`、`WOWSUNPACK`（自带 wowsunpack.exe 时）。
- [ ] 确认输出概要里 `new` 数量 ≥ 上一版本，且 `version` 为最新构建号。
- [ ] 抽查新增舰船的中文名正确、无 `(< 日期)` 之类后缀残留。
- [ ] 提交 `src/SeaMonkeys.App/Assets/ships.json` 的变更。

## 3. 构建与产物（必须）

- [ ] 本地构建发布产物（一键，含便携 zip 与 Inno 安装包）：
      ```
      pwsh -File build/build-release.ps1 -Variant all -Arch x64
      ```
      产物在 `artifacts/`：merged/split 各出便携 zip + 安装 exe（arm64 只出便携）。
- [ ] 版本号与 tag 一致（CI 会校验 tag 与 `Directory.Build.props` 的 `SeaMonkeysVersion`）。
- [ ] 确认运行目录不含 AI/ML/Widgets/WebView2 等无关组件（清理目标生效）。
- [ ] 确认合并版含 `WinUIEdit.dll`（删除会导致所有 TextBox 实例化失败并崩溃）；分离版不需要。
- [ ] 语言目录仅保留 zh/en（含 `en-GB`、`en-us`、`zh-CN`）。
- [ ] 编写变更说明：`.github/release-notes/<version>.md`（缺失则回退到 Full Changelog 链接）。
- [ ] 分离版安装包会检测并引导安装 .NET 10 桌面运行时与 Windows App Runtime 2.x。

## 4. 冒烟测试（必须）

- [ ] 应用启动，内存占用正常（约 80–130MB）。
- [ ] 战场页：加载对局、卡片、印章、配色、左键详情浮窗、右键观察名单。
- [ ] 历史页、观察名单页、设置页可正常打开。
- [ ] 深浅色切换后所有文本颜色正确（尤其等级、公会名、状态文本）。
- [ ] 船名表版本显示为最新，新船名称正确。
- [ ] 回放渲染页：预览单帧 → 导出视频。
- [ ] 对局观察：游戏开局写入 `tempArenaInfo.json` 后自动加载。

## 5. 发布（必须）

- [ ] 提交所有变更（含刷新后的 `ships.json` 与本版本变更说明）。
- [ ] 打 tag（形如 `v0.1.0`）并推送，触发 `.github/workflows/release.yml`。
- [ ] 等待 CI 构建全部通过，Release 自动发布（含产物与自动生成的说明）。
- [ ] 在 Release 说明中标注本次船名表版本与游戏构建号（写在 `.github/release-notes/<version>.md`）。
- [ ] 回传最新版下载链接（Windows / ARM64）。

## 关联

- `build/build-release.ps1`：一键构建发布产物（便携 zip + Inno 安装包）。
- `build/installer.iss`：Inno Setup 安装脚本（含分离版运行时引导）。
- `build/update-ships.ps1`：刷新内置船名表。
- `build/run-dev.ps1`：本地开发循环（停进程 → 构建 → 启动）。
- `build/gen-stamps.ps1` / `build/gen-appicon.ps1`：印章与应用图标生成。
- `.github/workflows/release.yml`：CI 发布流程。
- `.github/scripts/gen-release-notes.ps1` + `.github/release-notes-template.md`：发行说明生成。
