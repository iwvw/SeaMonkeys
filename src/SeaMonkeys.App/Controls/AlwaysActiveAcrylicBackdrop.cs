using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace SeaMonkeys.App.Controls;

/// <summary>
/// 始终活跃的亚克力背景：把 IsInputActive 固定为 true，窗口失焦或被遮挡时依然模糊下方内容。
/// </summary>
public sealed class AlwaysActiveAcrylicBackdrop : SystemBackdrop, IDisposable
{
    private readonly Dictionary<ICompositionSupportsSystemBackdrop, Target> targets = new();
    private bool disposed;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        var config = BuildConfig(xamlRoot);
        var controller = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Base };
        controller.SetSystemBackdropConfiguration(config);
        controller.AddSystemBackdropTarget(connectedTarget);

        targets[connectedTarget] = new Target(controller, config);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);

        if (targets.Remove(disconnectedTarget, out var target))
        {
            try
            {
                target.Controller.RemoveSystemBackdropTarget(disconnectedTarget);
            }
            catch
            {
            }
            target.Controller.Dispose();
        }
    }

    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        if (targets.TryGetValue(target, out var entry))
        {
            entry.Config.Theme = ResolveTheme(xamlRoot);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (var kv in targets)
        {
            try
            {
                kv.Value.Controller.RemoveSystemBackdropTarget(kv.Key);
            }
            catch
            {
            }
            kv.Value.Controller.Dispose();
        }
        targets.Clear();
    }

    private static SystemBackdropConfiguration BuildConfig(XamlRoot xamlRoot) => new()
    {
        IsInputActive = true,
        Theme = ResolveTheme(xamlRoot),
    };

    private static SystemBackdropTheme ResolveTheme(XamlRoot xamlRoot) =>
        xamlRoot.Content is FrameworkElement fe
            ? fe.ActualTheme switch
            {
                ElementTheme.Dark => SystemBackdropTheme.Dark,
                ElementTheme.Light => SystemBackdropTheme.Light,
                _ => SystemBackdropTheme.Default,
            }
            : SystemBackdropTheme.Default;

    private sealed class Target
    {
        public Target(DesktopAcrylicController controller, SystemBackdropConfiguration config)
        {
            Controller = controller;
            Config = config;
        }

        public DesktopAcrylicController Controller { get; }

        public SystemBackdropConfiguration Config { get; }
    }
}
