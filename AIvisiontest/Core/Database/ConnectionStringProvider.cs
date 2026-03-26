using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace AIvisiontest.Core.Database
{
    /// <summary>
    /// 从 app.config 读取数据库连接字符串。
    /// 需要 NuGet 包：System.Configuration.ConfigurationManager
    /// </summary>
    public static class ConnectionStringProvider
    {
        /// <summary>
        /// 读取 app.config 中 connectionStrings 节点下指定名称的连接字符串。
        /// 默认读取名为 "AppDb" 的连接字符串。
        /// </summary>
        /// <param name="name">connectionStrings 中的 name 属性，默认 "AppDb"</param>
        public static string Get(string name = "AppDb")
        {
            var entry = ConfigurationManager.ConnectionStrings[name];

            if (entry == null)
                throw new InvalidOperationException(
                    $"app.config 中未找到名为 \"{name}\" 的连接字符串，" +
                    $"请检查 <connectionStrings> 节点配置。");

            if (string.IsNullOrWhiteSpace(entry.ConnectionString))
                throw new InvalidOperationException(
                    $"app.config 中 \"{name}\" 的连接字符串为空。");

            return entry.ConnectionString;
        }
    }
}
