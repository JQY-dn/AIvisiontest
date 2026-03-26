using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static OpenCvSharp.ML.DTrees;
using Opc.Ua;
using Opc.Ua.Client;
using System.Threading;

namespace AIvisiontest.opc
{
    public class OPCUAClient
    {
        // 单例实例（线程安全）
        private static readonly Lazy<OPCUAClient> _instance = new Lazy<OPCUAClient>(() => new OPCUAClient());
        public static OPCUAClient Instance => _instance.Value;

        // OPC 核心对象
        private Session _session;
        private ApplicationConfiguration _config;
        private readonly object _lockObj = new object(); // 线程安全锁

        /// <summary>
        /// 连接状态
        /// </summary>
        public bool IsConnected => _session != null && _session.Connected;

        /// <summary>
        /// 连接 OPC UA 服务器（适配1.5+ SDK，替换弃用API）
        /// </summary>
        /// <param name="serverUrl">服务器地址（如 opc.tcp://192.168.1.100:4840）</param>
        /// <param name="username">用户名（无则传空）</param>
        /// <param name="password">密码（无则传空）</param>
        public async Task<bool> ConnectAsync(string serverUrl, string username = "", string password = "")
        {
            // 加锁避免多线程重复连接
            lock (_lockObj)
            {
                if (IsConnected) return true;
            }

            try
            {
                // 1. 初始化配置（兼容1.5+版本）
                _config = new ApplicationConfiguration
                {
                    ApplicationName = "PCB_Detection_Client",
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        AutoAcceptUntrustedCertificates = true, // 测试环境跳过证书验证（生产需配置证书）
                                                                // 1.5+版本：SuppressNonceValidation移到SessionSecurityConfiguration，此处无需配置
                    },
                    TransportQuotas = new TransportQuotas { OperationTimeout = 10000 },
                    ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },

                };
                await _config.ValidateAsync(ApplicationType.Client);

                // 2. 发现并选择端点（1.5+版本推荐的规范方式）
                var selectedEndpoint = await DiscoverEndpointAsync(serverUrl);
                var configuredEndpoint = new ConfiguredEndpoint(null, selectedEndpoint);

                // 3. 创建用户身份
                byte[] passwordBytes = string.IsNullOrEmpty(password) ? Array.Empty<byte>() : System.Text.Encoding.UTF8.GetBytes(password);
                IUserIdentity userIdentity = string.IsNullOrEmpty(username) ? new UserIdentity() : new UserIdentity(username, passwordBytes);

                // 4. 使用正确的 CreateAsync 重载：添加缺失的 checkDomain 参数并传入 CancellationToken
                //    
                _session = await Session.CreateAsync(
                    _config,                   // ApplicationConfiguration
                    null,        // ConfiguredEndpoint
                    configuredEndpoint,        // updateBeforeConnect
                    false,                     // checkDomain - 插入以匹配可用重载（通常为 false）
                    false,                    // updateBeforeConnect（连接前是否更新端点，设为false）
                    "PCB_Detection_Session",   // sessionName
                    60000u,                    // sessionTimeout (uint)
                    userIdentity,              // IUserIdentity
                    new List<string>(),        // preferredLocales
                    CancellationToken.None     // CancellationToken
                );

             


                Console.WriteLine("OPC UA 连接成功，SessionId：" + _session.SessionId);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OPC 连接失败：{ex.Message}");
                // 连接失败时清理资源
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// 辅助方法：发现服务端端点（1.5+版本规范）
        /// </summary>
        /// <summary>
        /// 终极方案：绕过DiscoveryClient，直接构造端点（适配所有早期SDK版本）
        /// </summary>
        /// <param name="serverUrl">服务器地址（如 opc.tcp://192.168.1.100:4840）</param>
        private async Task<EndpointDescription> DiscoverEndpointAsync(string serverUrl)
        {
            // 1. 校验输入地址合法性
            if (string.IsNullOrEmpty(serverUrl) || !serverUrl.StartsWith("opc.tcp://"))
            {
                throw new ArgumentException("无效的OPC UA服务器地址，格式应为：opc.tcp://IP:端口", nameof(serverUrl));
            }

            // 2. 手动构造端点描述（完全绕过DiscoveryClient，适配所有SDK版本）
            var endpoint = new EndpointDescription
            {
                // 核心：服务器地址
                EndpointUrl = serverUrl,
                // 安全模式：测试环境用None（无安全策略），生产环境可改为Sign/SignAndEncrypt
                SecurityMode = MessageSecurityMode.None,
                // 安全策略：无安全策略（匹配SecurityMode.None）
                SecurityPolicyUri = "http://opcfoundation.org/UA/SecurityPolicy#None",
                // 服务器应用描述（必填，随便填不影响连接）
                Server = new ApplicationDescription
                {
                    ApplicationName = new LocalizedText("OPC UA Server"),
                    ApplicationUri = serverUrl,
                    ApplicationType = ApplicationType.Server
                },
                // 传输配置（必填，默认值即可）
                TransportProfileUri = Profiles.UaTcpTransport,
                // 安全级别（默认0）
                SecurityLevel = 0
            };

            // 3. 返回构造好的端点（异步包装，保持方法签名一致）
            return await Task.FromResult(endpoint);
        }

        /// <summary>
        /// 写入字符串数据到 OPC 节点（同步方法，内部兼容异步）
        /// </summary>
        /// <param name="nodeId">节点ID（如 ns=2;s=AlgorithmConfig）</param>
        /// <param name="value">要写入的值</param>
        public bool WriteString(string nodeId, string value)
        {
            if (!IsConnected) return false;

            try
            {
                var node = new NodeId(nodeId);
                var writeValue = new WriteValue
                {
                    NodeId = node,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))// 显式指定类型，避免类型转换问题
                };
                // 使用正确的 WriteAsync 调用，传入 WriteValueCollection 并指定 CancellationToken
                _session.WriteAsync(null, new WriteValueCollection { writeValue }, CancellationToken.None).Wait();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入 OPC 节点失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从 OPC 节点读取字符串数据（同步方法，内部兼容异步）
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>读取的值（失败返回空）</returns>
        public string ReadString(string nodeId)
        {
            if (!IsConnected) return null;

            try
            {
                var node = new NodeId(nodeId);
                // 1.5+版本推荐使用异步ReadValue（同步ReadValue已逐步弃用）
                var value = _session.ReadValueAsync(node).Result;
                return value?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取 OPC 节点失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 异步写入字符串（推荐使用，替代同步方法）
        /// </summary>
        public async Task<bool> WriteStringAsync(string nodeId, string value)
        {
            if (!IsConnected) return false;

            try
            {
                var node = new NodeId(nodeId);
                var writeValue = new WriteValue
                {
                    NodeId = node,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                };
                await _session.WriteAsync(null, new WriteValueCollection { writeValue }, CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"异步写入 OPC 节点失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 异步读取字符串（推荐使用，替代同步方法）
        /// </summary>
        public async Task<string> ReadStringAsync(string nodeId)
        {
            if (!IsConnected) return null;

            try
            {
                var node = new NodeId(nodeId);
                var value = await _session.ReadValueAsync(node);
                return value?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"异步读取 OPC 节点失败：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 断开 OPC 连接（适配1.5+版本的异步Close）
        /// </summary>
        public async Task DisconnectAsync()
        {
            lock (_lockObj)
            {
                if (_session == null) return;
            }

            try
            {
                if (_session.Connected)
                {
                    await _session.CloseAsync(); // 1.5+版本推荐异步Close
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"关闭OPC连接失败：{ex.Message}");
            }
            finally
            {
                _session?.Dispose();
                _session = null;
            }
        }

        /// <summary>
        /// 兼容原有同步断开方法（内部调用异步）
        /// </summary>
        public void Disconnect()
        {
            DisconnectAsync().Wait();
        }
    }
}
