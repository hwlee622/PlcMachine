using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CommInterface
{
    public class CommInterfaceTcpListenerSingle : Comm
    {
        private string m_ipAddress;
        private int m_port;

        private TcpListener m_tcpListner;
        private TcpClient m_tcpClient;
        private NetworkStream m_networkStream;

        #region Constructors

        public CommInterfaceTcpListenerSingle(string ipAddress, int port)
        {
            m_ipAddress = ipAddress;
            m_port = port;
        }

        #endregion Constructors

        public override void Start()
        {
            Stop();

            IPAddress.Parse(m_ipAddress);
            m_tcpListner = new TcpListener(IPAddress.Any, m_port);
            m_tcpListner.Start();

            base.Start();
            Task.Run(() => OpenListnerTask(m_cts.Token));
        }

        public override void Stop()
        {
            base.Stop();
            m_networkStream?.Close();
            m_tcpClient?.Close();
            m_tcpListner?.Stop();
        }

        public override bool IsConnected()
        {
            bool isConnected = (m_tcpClient != null && m_networkStream != null && m_tcpClient.Connected);
            return isConnected;
        }

        protected override async Task ConnectTask(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(20);
                    bool isConnected = IsConnected();
                    UpdateConnectState(isConnected);
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            }
        }

        private async Task OpenListnerTask(CancellationToken token)
        {
            Task cancel = Task.Delay(Timeout.Infinite, token);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var accept = m_tcpListner.AcceptTcpClientAsync();
                    await Task.WhenAny(accept, cancel);
                    if (token.IsCancellationRequested)
                        continue;
                    else
                    {
                        TcpClient client = accept.Result;
                        IPAddress clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                        if (IsConnected() || !IsAllowIPAddress(clientIP))
                        {
                            client.Close();
                        }
                        else
                        {
                            m_tcpClient = client;
                            m_networkStream = client.GetStream();
                            m_connectExceptionDict.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            }
        }

        private bool IsAllowIPAddress(IPAddress clientIP)
        {
            IPAddress.TryParse(m_ipAddress, out IPAddress allowIP);
            bool isAllowIP = Equals(clientIP, allowIP);
            return isAllowIP;
        }

        protected override async Task WriteAsync(byte[] message)
        {
            OnSendLog?.Invoke(message);
            m_networkStream.WriteTimeout = WriteTimeout;
            await m_networkStream.WriteAsync(message, 0, message.Length);
        }

        protected override async Task ReadAsync()
        {
            var buffer = new byte[m_tcpClient.ReceiveBufferSize];
            int bufferLength = await m_networkStream.ReadAsync(buffer, 0, buffer.Length);
            if (bufferLength > 0)
            {
                var message = new byte[bufferLength];
                Array.Copy(buffer, message, bufferLength);
                OnReceiveLog?.Invoke(message);
                m_recvQueue.Enqueue(message);
            }
        }
    }
}