using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace YJComm
{
    public class CommTcpClient : Comm
    {
        private string m_ipAddress;
        private int m_port;

        private TcpClient m_tcpClient;
        private NetworkStream m_networkStream;

        #region Constructors

        public CommTcpClient(string ipAddress, int port)
        {
            m_ipAddress = ipAddress;
            m_port = port;
        }

        #endregion Constructors

        public override void Start()
        {
            Stop();

            IPAddress.Parse(m_ipAddress);

            base.Start();
        }

        public override void Stop()
        {
            base.Stop();

            m_networkStream?.Close();
            m_tcpClient?.Close();
        }

        public override bool IsConnected()
        {
            bool isConnected = m_tcpClient != null && m_networkStream != null && m_tcpClient.Connected;
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
                        m_tcpClient?.Dispose();
                        m_tcpClient = new TcpClient();
                        await Task.WhenAny(m_tcpClient.ConnectAsync(m_ipAddress, m_port), cancel);
                        if (token.IsCancellationRequested)
                            continue;

                        m_networkStream = m_tcpClient.GetStream();
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
            m_networkStream.WriteTimeout = WriteTimeout;
            await m_networkStream.WriteAsync(message, 0, message.Length);
        }

        protected override async Task ReadAsync()
        {
            var buffer = new byte[m_tcpClient.ReceiveBufferSize];
            var bufferLength = await m_networkStream.ReadAsync(buffer, 0, buffer.Length);
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