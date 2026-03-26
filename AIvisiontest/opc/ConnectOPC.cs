using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AIvisiontest.opc
{
    public class ConnectOPC
    {
        public static async Task Connection(string opcuri,string username,string password)
        {
            // 1. 获取单例客户端
            var opcClient = OPCUAClient.Instance;

            // 2. 连接服务器（替换为你的实际地址/账号）
            bool isConnected = await opcClient.ConnectAsync(
                opcuri,
                username,
                password);

            if (isConnected)
            {
                MessageBox.Show("连接成功");
            }
            else
            {
                MessageBox.Show("连接失败");
            }
        }
    }
}
