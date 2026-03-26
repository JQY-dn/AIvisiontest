using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIvisiontest.Core.Logging
{
    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        /// <summary>调试信息（仅开发阶段）</summary>
        Debug = 0,

        /// <summary>一般信息</summary>
        Info = 1,

        /// <summary>警告信息</summary>
        Warning = 2,

        /// <summary>错误信息</summary>
        Error = 3,

        /// <summary>严重错误（致命异常）</summary>
        Fatal = 4
    }
}
