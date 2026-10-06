# 参考：WinUI 3 前端踩坑（来自 sox / momomi 实践）

> 来源：`E:\Code\sox`（docs/handoff 的「关键坑」、ADR-0019）与 `E:\Code\momomi`（release notes、构建脚本）。
> 适用范围：SeaMonkeys 前端（`SeaMonkeys.App`）与打包发布。
> 后端（数据源、代理）无同类可复用经验，另见 `wows-data-sources.md`。

## 一、构建与发布

1. **必须按平台/RID 构建**。`WindowsAppSDKSelfContained=true` + AnyCPU 报 `requires a supported Windows architecture`。用 `-p:Platform=x64 -p:RuntimeIdentifier=win-x64`（或 arm64）。
2. **WinUI 3 不能 `PublishTrimmed`**。XAML 依赖反射，裁剪后启动 `XamlParseException`。`PublishReadyToRun` 也默认关（体积增数十 MB，收益小）。体积改用发布后删除无用运行时组件。
3. **`dotnet publish -o <目录>` 会丢 WinUI 编译资源**。`*.pri`、`*.xbf`、`Assets\` 只进 build 输出不进 publish，会启动崩溃（`Microsoft.UI.Xaml.dll` / `0xc000027b`）。需 target 从 `$(OutDir)` 显式补拷。
4. **WindowsAppSDK 元包强拉 AI/ML/Widgets**（onnxruntime + DirectML 约 45MB）。用直接引用 + `ExcludeAssets="all"` 排除。
5. **运行中的 exe 锁定自己**，build 前必须停进程。
6. **版本号单一来源**放 `Directory.Build.props`，CI 用 `-p:Version` 覆盖。
7. **数据目录**：便携版放 exe 旁 `data\`，不可写回退 `%LOCALAPPDATA%`；**安装与更新必须跳过数据目录**（对应 CONTEXT 不变量：更新绝不覆盖用户数据）。
8. **自更新**：公开仓库走匿名 GitHub Releases API + 镜像链；私有仓库匿名返回 404 需 token（sox 转公开后整条移除）。
9. **双形态发布**：merged（自包含）与 split（框架依赖，体积小约 100MB）。分离版安装包需引导安装 .NET 10 Desktop Runtime 与 Windows App Runtime 2.x（检测注册表在 `HKLM\SOFTWARE\Classes\Local Settings\...\PackageRepository\Packages\Microsoft.WindowsAppRuntime.2_*`）。
10. **Inno Setup 的 `DownloadTemporaryFile` 出错抛异常**（不返回 False），返回 Int64；已装更高版本时退出码 1638 视为成功。

## 二、窗口与材质（雷达悬浮面板直接相关）

11. **透明 margin 会吞点击**。`ShadowPadding > 0` 时窗口比卡片大，透明区仍参与命中测试。**面板窗口必须等于卡片大小**（sox 文件对话框面板踩过，我们悬浮面板同理）。
12. **DWM backdrop 需要非 layered 窗口**。`AllowsTransparency=True` 会让亚克力失效，用 `SingleBorderWindow` + `WindowChrome`，或 WinUI 3 的 `SystemBackdropElement`。
13. **窗口常驻 + DWM cloak** 显隐，不重建窗口（呼出 < 100ms 的关键）。复用 sox `WindowCloak`。
14. **`owned window`（`GWLP_HWNDPARENT`）优于 `WS_EX_TOPMOST`**。置顶会盖住所有应用；owned 只跟随宿主。雷达面板要结合全屏检测权衡。
15. **独占全屏下普通置顶窗会被遮挡**，这是可接受边界（提示用户用窗口化/无边框）。
16. **WinUI 3 冷启动比 WPF 慢**（CmdPal 实测 2.84s），靠常驻 cloak 抵消，不要为冷启动速度纠结。

## 三、托盘

17. **用 `H.NotifyIcon.WinUI` 2.5.0-beta.3 + `ContextMenuMode.SecondWindow`** 才有 Fluent 菜单（圆角/材质/主题跟随）。2.4.1 的 `SecondWindow` 首次弹出宽度偏窄（未预热 Flyout 测量返回 0）。
18. 托盘图标用**多尺寸 `.ico`**，库按 DPI 选尺寸避免模糊。
19. `TaskbarIcon` 是 `Application.Current.Resources` 里的**单例**，不要重复 `ForceCreate`。
20. `SecondWindow` 是库标注的 **preview**：多屏 / 高 DPI / 任务栏在侧或顶时可能定位异常，需实测。

## 四、WinUI 控件与线程

21. **`TextBlock` 是 sealed**，不能继承做自定义控件，高亮用附加属性方案（sox `TextHighlighter`）。
22. **`IShellItemImageFactory` 是 STA-only**，线程池（MTA）调用抛 `RPC_E_WRONG_THREAD (0x8001010E)`，需专用 STA 线程。
23. **`BitmapImage` 必须在 UI 线程创建**（COM 出 PNG 字节 → 回 UI 线程 `SetSource`）。
24. **`ItemsSource=null` 重建列表会闪**，改用 `ObservableCollection` 原位替换。
25. **自定义 `Program.cs` 必须加 `DISABLE_XAML_GENERATED_MAIN`**，否则入口冲突（CS0101）。
26. **XAML 根元素必须与 code-behind 基类一致**（如 `WindowEx` 要写 `<winuiex:WindowEx>`），否则 CS0263。
27. **`DispatcherQueueTimer` 命名冲突**：`Microsoft.UI.Dispatching` 与 `Windows.System` 都有，需完全限定。
28. **回收容器动画残留**：虚拟化 `Loaded` 的非动画分支要复位 opacity/transform。
29. **`SvgImageSource` 无 `SetSource`**，用 `UriSource` 指向临时 svg 文件。
30. **`DisplayArea` 是 `IReadOnlyList`，无 `IndexOf`**；多屏定位用 `GetCursorPos` + `DisplayArea.GetFromPoint`。
31. **`SearchStatusStream` 类周期推送**（每 ~5s）需按 signature 去重，否则日志刷屏。

## 五、进程与权限

32. **普通用户权限即可**，不需要管理员（与 sox 的索引服务不同）。
33. **自启用注册表 Run 值**即可（App 不提权时），提权应用才需要计划任务。
34. **改核心/插件后必须停服务**再 rebuild（sox 场景；SeaMonkeys 是否有多进程待定）。

## 六、开发工作流

35. **PowerShell 不支持 heredoc**，写文件用工具而非 `cat > ... << EOF`。
36. **残留的旧崩溃日志会误导**，排查前先删。
37. **多项目构建的全局属性泄漏**：publish 会触发内层项目构建，需显式 pin `SelfContained`/`Platform`，否则空 RID + `SelfContained=true` 触发 `NETSDK1191`。

## 与后端风险的关系

以上全部是前端问题，且都有成熟解法与可复用代码。SeaMonkeys 的**未知风险集中在后端数据源与代理**（见 `wows-data-sources.md`）：Vortex 未文档化、限流、隐藏档案、跨区探测、`X-Requested-With` 头。前端可按本清单低成本落地，后端需自行验证。
