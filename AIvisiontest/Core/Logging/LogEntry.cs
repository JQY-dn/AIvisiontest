using System;
using System.Windows.Media;

namespace AIvisiontest.Core.Logging
{
    /// <summary>
    /// UI 日志条目，绑定到 ListBox
    /// </summary>
    public class LogEntry
    {
        public DateTime Time { get; init; } = DateTime.Now;
        public LogLevel Level { get; init; }
        public string Tag { get; init; } = "";
        public string Message { get; init; } = "";

        /// <summary>格式化时间显示</summary>
        public string TimeText => Time.ToString("HH:mm:ss");

        /// <summary>级别标签</summary>
        public string LevelText => Level switch
        {
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Fatal => "FTL",
            _ => "???"
        };

        /// <summary>根据日志级别返回对应颜色</summary>
        public Brush LevelColor => Level switch
        {
            LogLevel.Debug => new SolidColorBrush(Color.FromRgb(120, 120, 120)), // 灰
            LogLevel.Info => new SolidColorBrush(Color.FromRgb(180, 210, 255)), // 淡蓝
            LogLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 200, 80)),  // 黄
            LogLevel.Error => new SolidColorBrush(Color.FromRgb(255, 90, 90)),  // 红
            LogLevel.Fatal => new SolidColorBrush(Color.FromRgb(255, 50, 50)),  // 深红
            _ => Brushes.White
        };

        /// <summary>消息文字颜色（Debug 偏暗，其他偏亮）</summary>
        public Brush MessageColor => Level == LogLevel.Debug
            ? new SolidColorBrush(Color.FromRgb(140, 140, 140))
            : new SolidColorBrush(Color.FromRgb(220, 220, 220));
    }
}
