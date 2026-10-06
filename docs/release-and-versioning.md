# 版本与发布

面向维护者。约定版本号、发行说明与自动构建的规则。

## 版本号

采用语义化版本 `主.次.补丁`（`x.y.z`），git tag 统一加 `v` 前缀。

| 位 | 何时递增 | 例子 |
|---|---|---|
| 主（x） | 架构性变更、不兼容改动 | `1.0.0` |
| 次（y） | 新增功能，向后兼容 | `0.2.0` |
| 补丁（z） | 修 bug、仅细节调整 | `0.1.1` |

**唯一来源**：`Directory.Build.props` 的 `<SeaMonkeysVersion>`。改版本只改这一处，其余全部自动派生：

- App / 安装包显示版本：`-p:Version=$SeaMonkeysVersion`（由 `build/build-release.ps1` 传入）
- 标题栏与关于页的版本文本来自程序集版本，随构建自动更新

递增用脚本，不要手改：

```powershell
pwsh -File build/bump-version.ps1 -Bump patch   # 0.1.0 -> 0.1.1
pwsh -File build/bump-version.ps1 -Bump minor   # 0.1.1 -> 0.2.0
pwsh -File build/bump-version.ps1 -Bump major   # 0.2.0 -> 1.0.0
pwsh -File build/bump-version.ps1 -Set 0.3.0    # 直接指定
```

## 发版流程

两种触发方式，都走同一个 `Release` 工作流。

### 方式 A：手动触发（推荐，自动 bump + 打 tag + 发布）

在 GitHub 的 Actions → Release → Run workflow 里选择：

| 输入 | 含义 |
|---|---|
| `bump` | `none` / `patch` / `minor` / `major`，选递增方式；`none` 表示用当前版本 |
| `version` | 填了则忽略 `bump`，直接以该版本发布 |

工作流会：递增 `Directory.Build.props` → 提交 → 打 `v<版本>` tag → 构建 → 生成说明 → 发布 Release。
（用 `GITHUB_TOKEN` 推送不会二次触发工作流，构建与发布在一次运行内完成。）

### 方式 B：手动打 tag

```powershell
pwsh -File build/bump-version.ps1 -Bump minor
# 可选：写变更条目 .github/release-notes/<version>.md
git commit -am "chore: release v0.2.0"
git tag v0.2.0
git push origin main --tags
```

tag 触发时工作流会校验 tag 与 `Directory.Build.props` 版本一致，不一致直接失败。

## 发行说明

两类模板，生成脚本按补丁位自动选择：

| 模板 | 适用 | 特征 |
|---|---|---|
| `release-notes-template-formal.md` | 正式版，补丁位为 0（`x.y.0`） | 完整下载表 + 选型建议 + 注意事项 |
| `release-notes-template-patch.md` | 小版本（`x.y.z`，z>0） | 精简下载表 |

变更正文优先取 `.github/release-notes/<version>.md`；没有则回退到与上一个 tag 的 compare 链接。

变更条目写法（简单平实，分节列出）：

```markdown
## 新增

- 一句话说清做了什么、对用户意味着什么。

## 变更

- ...

## 修复

- ...
```

本地手动生成：

```powershell
pwsh -File .github/scripts/gen-release-notes.ps1 `
  -Version 0.2.0 -ArtifactsDir artifacts `
  -OutputPath release-notes.md -PreviousTag v0.1.0
```

## 发行变体

每个架构产出两个运行时变体（`merged` / `split`），命名格式：

```
SeaMonkeys-<version>-<arch>-<variant>-portable.zip
SeaMonkeys-<version>-x64-<variant>-setup.exe
```

| 变体 | 运行时 | x64 体积量级 | 要求 |
|---|---|---|---|
| `merged`（合并版） | 随包携带 | 约 64MB | 无，开箱即用 |
| `split`（分离版） | 框架依赖 | 约 11MB | 已装 .NET 10 Desktop Runtime + Windows App SDK Runtime（安装包自动引导） |

本地按变体构建：

```powershell
pwsh -File build/build-release.ps1 -Variant merged -Arch x64
pwsh -File build/build-release.ps1 -Variant split  -Arch x64
pwsh -File build/build-release.ps1 -Variant all    -Arch x64   # 两种都出
```

## 工作流

| 文件 | 触发 | 作用 |
|---|---|---|
| `.github/workflows/ci.yml` | push / PR 到 `main` | 构建校验（Release / x64）+ 发布脚本冒烟 |
| `.github/workflows/release.yml` | 推 `v*` tag / 手动 | bump（手动）→ 出产物 → 生成说明 → 创建 Release |

## 发布前清单

见 `docs/release-checklist.md`。重点：先刷新船名表（`build/update-ships.ps1`），再写变更条目，最后触发发布。
