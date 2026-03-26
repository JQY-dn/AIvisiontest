using AIvisiontest.Views;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace AIvisiontest.Core.Services
{
    // ── Toast 类型枚举 ─────────────────────────────────────────
    public enum ToastType
    {
        Loading,   // 蓝色转圈，无自动关闭
        Success,   // 绿色勾，默认 4s 后关闭
        Error,     // 红色叉，默认 6s 后关闭
    }

    // ── Toast 句柄：持有对窗口的引用，可手动关闭 ──────────────
    public sealed class ToastHandle
    {
        private readonly ToastWindow _window;

        internal ToastHandle(ToastWindow window)
        {
            _window = window;
        }

        /// <summary>手动关闭此 Toast（例如加载完成后关闭 Loading）</summary>
        public void Dismiss()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _window.DismissWithAnimation();
            });
        }
    }

    // ── 核心服务 ────────────────────────────────────────────────
    /// <summary>
    /// 全局 Toast 通知服务。
    /// 必须在 UI 线程（或通过 Dispatcher）调用。
    /// </summary>
    public static class ToastService
    {
        // 同时最多显示的 Toast 数量（超出时移除最旧的）
        private const int MaxVisible = 3;

        // 各类型默认自动消失时间（秒），0 = 不自动消失
        private static readonly Dictionary<ToastType, int> DefaultDurations = new()
        {
            { ToastType.Loading, 0 },   // Loading 必须手动关闭
            { ToastType.Success, 4 },
            { ToastType.Error,   6 },
        };

        private static readonly List<ToastWindow> _visible = new();
        private static readonly object _lock = new();

        // ── 公开 API ────────────────────────────────────────────

        /// <summary>
        /// 显示一条 Toast。
        /// </summary>
        /// <param name="type">类型（Loading / Success / Error）</param>
        /// <param name="title">主标题</param>
        /// <param name="desc">副标题（文件名/错误信息，可为空）</param>
        /// <param name="autoDismiss">
        ///   自动消失秒数；传 -1 则使用该类型的默认值
        /// </param>
        /// <returns>Toast 句柄，用于手动关闭</returns>
        public static ToastHandle Show(
            ToastType type,
            string title,
            string? desc = null,
            int autoDismiss = -1)
        {
            // 确保在 UI 线程
            if (Application.Current?.Dispatcher is not Dispatcher dispatcher)
                throw new InvalidOperationException("ToastService 需要 WPF Application 环境");

            ToastHandle handle = null!;

            dispatcher.Invoke(() =>
            {
                PruneIfNeeded();

                int duration = autoDismiss >= 0 ? autoDismiss : DefaultDurations[type];

                var window = new ToastWindow();
                window.Configure(type, title, desc, duration);

                // 注册移除
                window.Closed += (_, _) =>
                {
                    lock (_lock) { _visible.Remove(window); }
                    RearrangeWindows();
                };

                lock (_lock) { _visible.Add(window); }

                window.Show();
                RearrangeWindows();

                handle = new ToastHandle(window);
            });

            return handle;
        }

        /// <summary>关闭所有当前显示的 Toast</summary>
        public static void DismissAll()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // 复制列表，避免枚举时修改
                var copy = new List<ToastWindow>(_visible);
                foreach (var w in copy) w.DismissWithAnimation();
            });
        }

        // ── 私有：位置管理 ─────────────────────────────────────

        /// <summary>
        /// 将多个 Toast 竖向排列在屏幕右下角，新的在最下方。
        /// </summary>
        private static void RearrangeWindows()
        {
            var area = SystemParameters.WorkArea;
            const double gap = 10;
            const double margin = 20;

            double top = area.Top + margin;

            foreach (var w in _visible)
            {
                w.UpdateLayout();
                double h = w.ActualHeight > 0 ? w.ActualHeight : 80;

                w.Left = area.Left + (area.Width - w.Width) / 2;  // 水平居中
                w.Top = top;

                top += h + gap;  // 下一条往下偏移
            }
        }

        /// <summary>如果超过最大数量，关闭最老的</summary>
        private static void PruneIfNeeded()
        {
            while (_visible.Count >= MaxVisible)
            {
                _visible[0].DismissWithAnimation();
                _visible.RemoveAt(0);
            }
        }
    }
}