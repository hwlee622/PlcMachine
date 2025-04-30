using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CommInterface
{
    public class CommInterfaceUdp : Comm
    {
        private string m_ipAddress;
        private int m_port;

        private UdpClient m_udpClient;

        #region Constructors

        public CommInterfaceUdp(string ipAddress, int port)
        {
            m_ipAddress = ipAddress;
            m_port = port;
        }

        #endregion Constructors

        public override void Start()
        {
            Stop();

            IPAddress.Parse(m_ipAddress);
            m_udpClient = new UdpClient(0);

            base.Start();
        }

        public override void Stop()
        {
            base.Stop();

            m_udpClient?.Close();
        }

        public override bool IsConnected()
        {
            bool isConnected = m_udpClient != null;
            return isConnected;
        }

        protected override async Task ConnectTask(CancellationToken token)
        {
            Task cancel = Task.Delay(Timeout.Infinite, token);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(20);
                    bool isConnected = IsConnected();
                    UpdateConnectState(isConnected);
                    if (token.IsCancellationRequested)
                        continue;
                    else if (!isConnected)
                    {
                        m_udpClient = new UdpClient(0);
                        m_connectExceptionDict.Clear();
                    }
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            }
        }

        protected override async Task WriteAsync(byte[] message)
        {
            OnSendLog?.Invoke(message);
            m_udpClient.Client.SendTimeout = WriteTimeout;
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(m_ipAddress), m_port);
            await m_udpClient.SendAsync(message, message.Length, endPoint);
        }

        protected override async Task ReadAsync()
        {
            var recvResult = await m_udpClient.ReceiveAsync();
            if (recvResult != null && recvResult.Buffer.Length > 0)
            {
                var message = new byte[recvResult.Buffer.Length];
                Array.Copy(recvResult.Buffer, message, recvResult.Buffer.Length);
                OnReceiveLog?.Invoke(message);
                m_recvQueue.Enqueue(message);
            }
        }
    }
}