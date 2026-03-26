using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIvisiontest.Core.Logging
{
    /// <summary>
    /// 日志服务接口，用于 Prism 依赖注入
    /// </summary>
    public interface ILogService
    {
        /// <summary>
        /// 在原有 ILogService 基础上，增加 UI 订阅事件。
        /// ViewModel 订阅 OnLogEntry 即可实时收到日志条目。
        /// </summary>
        /// <summary>当前最低日志输出级别</summary>
        LogLevel MinimumLevel { get; set; }

        void Debug(string message, string? tag = null);
        void Info(string message, string? tag = null);
        void Warning(string message, string? tag = null);
        void Error(string message, Exception? ex = null, string? tag = null);
        void Fatal(string message, Exception? ex = null, string? tag = null);

        /// <summary>写入任意级别日志</summary>
        void Log(LogLevel level, string message, Exception? ex = null, string? tag = null);

        /// <summary>
        /// 每当有新日志写入时触发，ViewModel 订阅此事件更新 UI 列表。
        /// 注意：回调在写日志的线程触发，更新 UI 时需要 Dispatcher 调度。
        /// </summary>
        event Action<LogEntry>? OnLogEntry;
    }
}
