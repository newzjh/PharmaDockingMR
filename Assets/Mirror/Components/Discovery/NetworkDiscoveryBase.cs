using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

// Based on https://github.com/EnlightenedOne/MirrorNetworkDiscovery
// forked from https://github.com/in0finite/MirrorNetworkDiscovery
// Both are MIT Licensed

namespace Mirror.Discovery
{
    /// <summary>
    /// Base implementation for Network Discovery.  Extend this component
    /// to provide custom discovery with game specific data
    /// <see cref="NetworkDiscovery">NetworkDiscovery</see> for a sample implementation
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-discovery")]
    public abstract class NetworkDiscoveryBase<Request, Response> : MonoBehaviour
        where Request : NetworkMessage
        where Response : NetworkMessage
    {
        public static bool SupportedOnThisPlatform { get { return Application.platform != RuntimePlatform.WebGLPlayer; } }

        [SerializeField]
        [Tooltip("If true, broadcasts a discovery request every ActiveDiscoveryInterval seconds")]
        public bool enableActiveDiscovery = true;

        // broadcast address needs to be configurable on iOS:
        // https://github.com/vis2k/Mirror/pull/3255
        [Tooltip("iOS may require LAN IP address here (e.g. 192.168.x.x), otherwise leave blank.")]
        public string BroadcastAddress = "";

        [SerializeField]
        [Tooltip("The UDP port the server will listen for multi-cast messages")]
        protected int serverBroadcastListenPort = 47777;

        [SerializeField]
        [Tooltip("Time in seconds between multi-cast messages")]
        [Range(1, 60)]
        float ActiveDiscoveryInterval = 3;

        [Tooltip("Transport to be advertised during discovery")]
        public Transport transport;

        [Tooltip("Invoked when a server is found")]
        public ServerFoundUnityEvent<Response> OnServerFound;

        // Each game should have a random unique handshake,
        // this way you can tell if this is the same game or not
        [HideInInspector]
        public long secretHandshake;

        public long ServerId { get; private set; }

        protected UdpClient serverUdpClient;
        protected UdpClient clientUdpClient;

#if UNITY_EDITOR
        public virtual void OnValidate()
        {
            if (transport == null)
                transport = GetComponent<Transport>();

            if (secretHandshake == 0)
            {
                secretHandshake = RandomLong();
                UnityEditor.Undo.RecordObject(this, "Set secret handshake");
            }
        }
#endif

        // 标记是否已申请过权限
        private bool _isPermissionRequested = false;


        /// <summary>
        /// 申请iOS本地网络权限（触发系统弹窗）
        /// </summary>
        public void RequestLocalNetworkPermission()
        {
            if (_isPermissionRequested) return;
            _isPermissionRequested = true;

            try
            {
                // 核心：创建Bonjour监听（空服务名即可触发权限弹窗）
                // 使用UDP监听模拟Bonjour行为，不会实际占用端口
                using (var udpClient = new UdpClient())
                {
                    udpClient.EnableBroadcast = true;
                    // 绑定到本地任意端口，触发系统权限检查
                    udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 5353)); // 5353是Bonjour默认端口
                    udpClient.Close();
                }

                Debug.Log("本地网络权限申请触发成功");
            }
            catch (Exception e)
            {
                // 捕获异常（如用户已拒绝权限），不影响流程
                Debug.LogWarning($"本地网络权限申请触发失败：{e.Message}");
            }
        }

        /// <summary>
        /// 检查是否已获得本地网络权限（可选）
        /// </summary>
        /// <returns>是否有权限</returns>
        public bool CheckLocalNetworkPermission()
        {
#if UNITY_IOS && !UNITY_EDITOR
        // iOS没有直接的C# API检查权限，需通过原生交互（见方式2）
        // 此处简化：尝试发送广播，能发送则代表有权限
        try
        {
            using (var udpClient = new UdpClient())
            {
                udpClient.EnableBroadcast = true;
                udpClient.Send(new byte[1], 1, new IPEndPoint(IPAddress.Broadcast, 0));
                return true;
            }
        }
        catch
        {
            return false;
        }
#else
            return true;
#endif
        }

        /// <summary>
        /// virtual so that inheriting classes' Start() can call base.Start() too
        /// </summary>
        public virtual void Start()
        {
            // 仅在iOS平台触发
#if UNITY_IOS && !UNITY_EDITOR
            RequestLocalNetworkPermission();
#endif

            ServerId = RandomLong();

            // active transport gets initialized in Awake
            // so make sure we set it here in Start() after Awake
            // Or just let the user assign it in the inspector
            if (transport == null)
                transport = Transport.active;

            // Server mode? then start advertising
            if (Utils.IsHeadless())
            {
                AdvertiseServer();
            }
        }

        public static long RandomLong()
        {
            int value1 = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            int value2 = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            return value1 + ((long)value2 << 32);
        }

        // Ensure the ports are cleared no matter when Game/Unity UI exits
        void OnApplicationQuit()
        {
            //Debug.Log("NetworkDiscoveryBase OnApplicationQuit");
            Shutdown();
        }

        void OnDisable()
        {
            //Debug.Log("NetworkDiscoveryBase OnDisable");
            Shutdown();
        }

        void OnDestroy()
        {
            //Debug.Log("NetworkDiscoveryBase OnDestroy");
            Shutdown();
        }

        void Shutdown()
        {
            EndpMulticastLock();
            if (serverUdpClient != null)
            {
                try
                {
                    serverUdpClient.Close();
                }
                catch (Exception)
                {
                    // it is just close, swallow the error
                }

                serverUdpClient = null;
            }

            if (clientUdpClient != null)
            {
                try
                {
                    clientUdpClient.Close();
                }
                catch (Exception)
                {
                    // it is just close, swallow the error
                }

                clientUdpClient = null;
            }

            CancelInvoke();
        }

        #region Server

        /// <summary>
        /// Advertise this server in the local network
        /// </summary>
        public void AdvertiseServer()
        {
            if (!SupportedOnThisPlatform)
                throw new PlatformNotSupportedException("Network discovery not supported in this platform");

            StopDiscovery();

            // Setup port -- may throw exception
            serverUdpClient = new UdpClient(serverBroadcastListenPort)
            {
                EnableBroadcast = true,
                MulticastLoopback = false
            };

            //Debug.Log($"Discovery: Advertising Server {Dns.GetHostName()}");

            // listen for client pings
            _ = ServerListenAsync();
        }

        public async Task ServerListenAsync()
        {
            BeginMulticastLock();
            while (true)
            {
                try
                {
                    await ReceiveRequestAsync(serverUdpClient);
                }
                catch (ObjectDisposedException)
                {
                    // socket has been closed
                    break;
                }
                catch (Exception) {}
            }
        }

        async Task ReceiveRequestAsync(UdpClient udpClient)
        {
            // only proceed if there is available data in network buffer, or otherwise Receive() will block
            // average time for UdpClient.Available : 10 us

            UdpReceiveResult udpReceiveResult = await udpClient.ReceiveAsync();

            using (NetworkReaderPooled networkReader = NetworkReaderPool.Get(udpReceiveResult.Buffer))
            {
                long handshake = networkReader.ReadLong();
                if (handshake != secretHandshake)
                {
                    // message is not for us
                    throw new ProtocolViolationException("Invalid handshake");
                }

                Request request = networkReader.Read<Request>();

                ProcessClientRequest(request, udpReceiveResult.RemoteEndPoint);
            }
        }

        /// <summary>
        /// Reply to the client to inform it of this server
        /// </summary>
        /// <remarks>
        /// Override if you wish to ignore server requests based on
        /// custom criteria such as language, full server game mode or difficulty
        /// </remarks>
        /// <param name="request">Request coming from client</param>
        /// <param name="endpoint">Address of the client that sent the request</param>
        protected virtual void ProcessClientRequest(Request request, IPEndPoint endpoint)
        {
            Response info = ProcessRequest(request, endpoint);

            if (info == null)
                return;

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                try
                {
                    writer.WriteLong(secretHandshake);

                    writer.Write(info);

                    ArraySegment<byte> data = writer.ToArraySegment();
                    // signature matches
                    // send response
                    serverUdpClient.Send(data.Array, data.Count, endpoint);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }
        }

        /// <summary>
        /// Process the request from a client
        /// </summary>
        /// <remarks>
        /// Override if you wish to provide more information to the clients
        /// such as the name of the host player
        /// </remarks>
        /// <param name="request">Request coming from client</param>
        /// <param name="endpoint">Address of the client that sent the request</param>
        /// <returns>The message to be sent back to the client or null</returns>
        protected abstract Response ProcessRequest(Request request, IPEndPoint endpoint);

        // Android Multicast fix: https://github.com/vis2k/Mirror/pull/2887
#if UNITY_ANDROID
        AndroidJavaObject multicastLock;
        bool hasMulticastLock;
#endif

        void BeginMulticastLock()
		{
#if UNITY_ANDROID
            if (hasMulticastLock) return;

            if (Application.platform == RuntimePlatform.Android)
            {
                using (AndroidJavaObject activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    using (var wifiManager = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
                    {
                        multicastLock = wifiManager.Call<AndroidJavaObject>("createMulticastLock", "lock");
                        multicastLock.Call("acquire");
                        hasMulticastLock = true;
                    }
                }
			}
#endif
        }

        void EndpMulticastLock()
        {
#if UNITY_ANDROID
            if (!hasMulticastLock) return;

            multicastLock?.Call("release");
            hasMulticastLock = false;
#endif
        }

#endregion

        #region Client

        /// <summary>
        /// Start Active Discovery
        /// </summary>
        public void StartDiscovery()
        {
            if (!SupportedOnThisPlatform)
                throw new PlatformNotSupportedException("Network discovery not supported in this platform");

            StopDiscovery();

            try
            {
                // Setup port
                clientUdpClient = new UdpClient(0)
                {
                    EnableBroadcast = true,
                    MulticastLoopback = false
                };
            }
            catch (Exception)
            {
                // Free the port if we took it
                //Debug.LogError("NetworkDiscoveryBase StartDiscovery Exception");
                Shutdown();
                throw;
            }

            _ = ClientListenAsync();

            if (enableActiveDiscovery) InvokeRepeating(nameof(BroadcastDiscoveryRequest), 0, ActiveDiscoveryInterval);
        }

        /// <summary>
        /// Stop Active Discovery
        /// </summary>
        public void StopDiscovery()
        {
            //Debug.Log("NetworkDiscoveryBase StopDiscovery");
            Shutdown();
        }

        /// <summary>
        /// Awaits for server response
        /// </summary>
        /// <returns>ClientListenAsync Task</returns>
        public async Task ClientListenAsync()
        {
            // while clientUpdClient to fix:
            // https://github.com/vis2k/Mirror/pull/2908
            //
            // If, you cancel discovery the clientUdpClient is set to null.
            // However, nothing cancels ClientListenAsync. If we change the if(true)
            // to check if the client is null. You can properly cancel the discovery,
            // and kill the listen thread.
            //
            // Prior to this fix, if you cancel the discovery search. It crashes the
            // thread, and is super noisy in the output. As well as causes issues on
            // the quest.
            while (clientUdpClient != null)
            {
                try
                {
                    await ReceiveGameBroadcastAsync(clientUdpClient);
                }
                catch (ObjectDisposedException)
                {
                    // socket was closed, no problem
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        /// <summary>
        /// 获取 iOS 兼容的广播地址（优先子网定向广播，降级为有限广播）
        /// </summary>
        /// <returns>有效的广播 IPAddress</returns>
        public static IPAddress GetIOSCompatibleBroadcastAddress()
        {
            try
            {
                System.Collections.Generic.List<IPAddress> addresss = new ();

                // 步骤 1：获取本机活跃网卡的子网信息（优先 WiFi/蜂窝网）
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    // 过滤有效网卡：启用、非回环、支持 IPv4
                    if (!ni.OperationalStatus.Equals(OperationalStatus.Up) ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        !ni.Supports(NetworkInterfaceComponent.IPv4))
                        continue;

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        // 仅处理 IPv4 地址
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        // 计算子网定向广播地址（IP + 子网掩码 取反）
                        var ipBytes = ua.Address.GetAddressBytes();
                        var maskBytes = ua.IPv4Mask?.GetAddressBytes();
                        if (maskBytes == null || maskBytes.Length != 4)
                            continue;

                        // 计算广播地址：(IP & 掩码) | (~掩码)
                        byte[] broadcastBytes = new byte[4];
                        for (int i = 0; i < 4; i++)
                        {
                            broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                        }

                        addresss.Add(new IPAddress(broadcastBytes));
                    }
                }

                foreach(var address in addresss)
                {
                    byte[] bytes = address.GetAddressBytes();
                    if (bytes!=null && bytes.Length>=4)
                    {
                        if (bytes[0] == 192 && bytes[1] == 168)
                            return address;
                    }
                }

                if (addresss.Count>0)
                    return addresss.FirstOrDefault();
            }
            catch (Exception ex)
            {
                // 异常时降级为 255.255.255.255
                Console.WriteLine($"获取子网广播地址失败：{ex.Message}");
            }

            // 降级方案：返回有限广播地址（255.255.255.255）
            return IPAddress.Broadcast;
        }

        /// <summary>
        /// Sends discovery request from client
        /// </summary>
        public void BroadcastDiscoveryRequest()
        {
            if (clientUdpClient == null)
                return;

            if (NetworkClient.isConnected)
            {
                StopDiscovery();
                return;
            }

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, serverBroadcastListenPort);

            if (!string.IsNullOrWhiteSpace(BroadcastAddress))
            {
                try
                {
                    endPoint = new IPEndPoint(IPAddress.Parse(BroadcastAddress), serverBroadcastListenPort);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

//#if UNITY_IOS
            if (string.IsNullOrWhiteSpace(BroadcastAddress))
            {
                try
                {
                    endPoint = new IPEndPoint(GetIOSCompatibleBroadcastAddress(), serverBroadcastListenPort);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
//#endif

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                writer.WriteLong(secretHandshake);

                try
                {
                    Request request = GetRequest();

                    writer.Write(request);

                    ArraySegment<byte> data = writer.ToArraySegment();

                    //Debug.Log($"Discovery: Sending BroadcastDiscoveryRequest {request}");
                    clientUdpClient.SendAsync(data.Array, data.Count, endPoint);
                }
                catch (Exception)
                {
                    // It is ok if we can't broadcast to one of the addresses
                }
            }
        }

        /// <summary>
        /// Create a message that will be broadcasted on the network to discover servers
        /// </summary>
        /// <remarks>
        /// Override if you wish to include additional data in the discovery message
        /// such as desired game mode, language, difficulty, etc... </remarks>
        /// <returns>An instance of ServerRequest with data to be broadcasted</returns>
        protected virtual Request GetRequest() => default;

        async Task ReceiveGameBroadcastAsync(UdpClient udpClient)
        {
            // only proceed if there is available data in network buffer, or otherwise Receive() will block
            // average time for UdpClient.Available : 10 us

            UdpReceiveResult udpReceiveResult = await udpClient.ReceiveAsync();

            using (NetworkReaderPooled networkReader = NetworkReaderPool.Get(udpReceiveResult.Buffer))
            {
                if (networkReader.ReadLong() != secretHandshake)
                    return;

                Response response = networkReader.Read<Response>();

                ProcessResponse(response, udpReceiveResult.RemoteEndPoint);
            }
        }

        /// <summary>
        /// Process the answer from a server
        /// </summary>
        /// <remarks>
        /// A client receives a reply from a server, this method processes the
        /// reply and raises an event
        /// </remarks>
        /// <param name="response">Response that came from the server</param>
        /// <param name="endpoint">Address of the server that replied</param>
        protected abstract void ProcessResponse(Response response, IPEndPoint endpoint);

#endregion
    }
}
