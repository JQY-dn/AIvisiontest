using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AIvisiontest.Core.Services;

namespace AIvisiontest.Views
{
    /// <summary>
    /// ToastWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ToastWindow : Window
    {

        private readonly DispatcherTimer _autoCloseTimer = new();
        private bool _isClosing;

        // ── 颜色常量 ──────────────────────────────────────
        private static readonly Color ColorLoading = (Color)ColorConverter.ConvertFromString("#3B82F6");
        private static readonly Color ColorSuccess = (Color)ColorConverter.ConvertFromString("#22C55E");
        private static readonly Color ColorError = (Color)ColorConverter.ConvertFromString("#EF4444");
        public ToastWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }
        // ── 公开配置入口 ──────────────────────────────────
        /// <summary>
        /// 配置 Toast 内容并启动入场动画
        /// </summary>
        public void Configure(ToastType type, string title, string? desc = null, int autoDismissSeconds = 0)
        {
            TitleText.Text = title;

            if (!string.IsNullOrWhiteSpace(desc))
            {
                DescText.Text = desc;
                DescText.Visibility = Visibility.Visible;
            }
            else
            {
                DescText.Visibility = Visibility.Collapsed;
            }

            ApplyType(type);

            // 自动关闭
            if (autoDismissSeconds > 0)
            {
                StartProgressBar(autoDismissSeconds);

                _autoCloseTimer.Interval = TimeSpan.FromSeconds(autoDismissSeconds);
                _autoCloseTimer.Tick += (_, _) => DismissWithAnimation();
                _autoCloseTimer.Start();
            }
            else
            {
                // 无限等待时隐藏进度条
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        // ── 外部调用：触发关闭 ────────────────────────────
        public void DismissWithAnimation()
        {
            if (_isClosing) return;
            _isClosing = true;
            _autoCloseTimer.Stop();

            var storyboard = (Storyboard)FindResource("SlideOutStoryboard");
            storyboard.Begin(this);
        }

        // ── 私有：应用 Toast 类型样式 ─────────────────────
        private void ApplyType(ToastType type)
        {
            // 全部先隐藏
            SpinIcon.Visibility = Visibility.Collapsed;
            SuccessIcon.Visibility = Visibility.Collapsed;
            ErrorIcon.Visibility = Visibility.Collapsed;

            switch (type)
            {
                case ToastType.Loading:
                    SpinIcon.Visibility = Visibility.Visible;
                    var spinSb = (Storyboard)FindResource("SpinStoryboard");
                    spinSb.Begin(this);
                    SetAccentColor(ColorLoading);
                    break;

                case ToastType.Success:
                    SuccessIcon.Visibility = Visibility.Visible;
                    SetAccentColor(ColorSuccess);
                    break;

                case ToastType.Error:
                    ErrorIcon.Visibility = Visibility.Visible;
                    SetAccentColor(ColorError);
                    break;
            }
        }

        private void SetAccentColor(Color color)
        {
            AccentBar.Fill = new SolidColorBrush(color);
            ProgressBrush.Color = color;
        }

        private void StartProgressBar(int seconds)
        {
            ProgressBar.Visibility = Visibility.Visible;

            // 动态设置动画时长
            var storyboard = (Storyboard)FindResource("ProgressStoryboard");
            var anim = (DoubleAnimation)storyboard.Children[0];
            anim.Duration = new Duration(TimeSpan.FromSeconds(seconds));
            storyboard.Begin(this);
        }

        // ── 事件处理 ──────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PositionWindow();
            var slideIn = (Storyboard)FindResource("SlideInStoryboard");
            slideIn.Begin(this);
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
            => DismissWithAnimation();

        private void SlideOut_Completed(object sender, EventArgs e)
            => Close();

        // ── 定位到屏幕右下角 ──────────────────────────────
        private void PositionWindow()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Left + (area.Width - Width) / 2;  // 水平居中
            Top = area.Top + 20;                          // 顶部 20px 间距
        }
    }
}
