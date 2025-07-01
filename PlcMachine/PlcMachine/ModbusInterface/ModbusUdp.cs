﻿using NModbus;
using System.Net.Sockets;

namespace ModbusInterface
{
    public class ModbusUdp : Modbus
    {
        private string m_ipAddress;
        private int m_port;

        private UdpClient m_udpClient;

        public ModbusUdp(string ip, int port) : base()
        {
            m_logWriter = new ModbusLogWriter(ip, port);

            m_ipAddress = ip;
            m_port = port;
        }

        public override void Start()
        {
            m_udpClient = new UdpClient(m_ipAddress, m_port);
            m_factory = new ModbusFactory();
            m_master = m_factory.CreateMaster(m_udpClient);
        }

        public override void Stop()
        {
            m_udpClient?.Close();
        }
    }
}
