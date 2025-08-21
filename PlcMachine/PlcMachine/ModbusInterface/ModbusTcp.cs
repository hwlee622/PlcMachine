using NModbus;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusInterface
{
    public class ModbusTcp : Modbus
    {
        private string m_ipAddress;
        private int m_port;

        private TcpClient m_tcpClient;

        private CancellationTokenSource m_cts = new CancellationTokenSource(0);
        private Dictionary<Type, bool> m_exceptionDict = new Dictionary<Type, bool>();

        public ModbusTcp(string ip, int port) : base()
        {
            m_logWriter = new ModbusLogWriter(ip, port);

            m_ipAddress = ip;
            m_port = port;
        }

        public override void Start()
        {
            if (m_cts.IsCancellationRequested)
                m_cts = new CancellationTokenSource();

            m_tcpClient = new TcpClient(m_ipAddress, m_port);
            m_tcpClient.ReceiveTimeout = ReadTimeout;
            var factory = new ModbusFactory();
            m_master = factory.CreateMaster(m_tcpClient);

            Task.Run(() => ConnectTask(m_cts.Token));
        }

        public override void Stop()
        {
            m_cts.Cancel();
            m_master?.Dispose();
            m_tcpClient?.Dispose();
        }

        private async void ConnectTask(CancellationToken token)
        {
            var cancel = Task.Delay(Timeout.Infinite, token);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(20);
                    if (token.IsCancellationRequested)
                        continue;

                    bool isConnected = m_tcpClient == null || m_tcpClient.Connected;
                    if (isConnected)
                        continue;

                    var tcpClient = new TcpClient(m_ipAddress, m_port);
                    tcpClient.ReceiveTimeout = ReadTimeout;
                    m_exceptionDict.Clear();
                    var factory = new ModbusFactory();
                    m_master = factory.CreateMaster(tcpClient);

                    m_tcpClient.Dispose();
                    m_tcpClient = tcpClient;
                }
                catch (Exception ex)
                {
                    var exType = ex.GetType();
                    if (!m_exceptionDict.ContainsKey(exType))
                    {
                        m_logWriter.LogError(ex);
                        m_exceptionDict[exType] = true;
                    }
                }
            }
        }
    }
}
