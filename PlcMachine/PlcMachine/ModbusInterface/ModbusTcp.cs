using NModbus;
using System.Net.Sockets;

namespace ModbusInterface
{
    public class ModbusTcp : Modbus
    {
        private string m_ipAddress;
        private int m_port;

        private TcpClient m_tcpClient;

        public ModbusTcp(string ip, int port) : base()
        {
            m_logWriter = new ModbusLogWriter(ip, port);

            m_ipAddress = ip;
            m_port = port;
        }

        public override void Start()
        {
            m_tcpClient = new TcpClient(m_ipAddress, m_port);
            m_tcpClient.ReceiveTimeout = ReadTimeout;
            m_factory = new ModbusFactory();
            m_master = m_factory.CreateMaster(m_tcpClient);
        }

        public override void Stop()
        {
            m_tcpClient?.Close();
        }
    }
}
