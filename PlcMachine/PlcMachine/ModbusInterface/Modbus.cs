using NModbus;
using System;
using System.Net.Sockets;
using System.Threading;

namespace ModbusInterface
{
    public class Modbus
    {
        private ModbusLogWriter m_logWriter;

        private string m_ipAddress;
        private const int PORT = 502;

        private TcpClient m_tcpClient;
        private UdpClient m_udpClient;
        private ModbusFactory m_factory;
        private IModbusMaster m_master;

        public int WriteTimeout = Timeout.Infinite;
        public int ReadTimeout = Timeout.Infinite;

        public Modbus(string ip)
        {
            m_logWriter = new ModbusLogWriter(ip, PORT);

            m_ipAddress = ip;
        }

        public void Start()
        {
            m_tcpClient = new TcpClient(m_ipAddress, PORT);
            m_tcpClient.ReceiveTimeout = ReadTimeout;
            m_factory = new ModbusFactory();
            m_master = m_factory.CreateMaster(m_tcpClient);
        }

        public void Stop()
        {
            m_tcpClient?.Close();
            m_udpClient?.Close();
        }

        public bool ReadCoil(ushort address, out bool data)
        {
            data = false;
            try
            {
                data = m_master.ReadCoils(1, address, 1)[0];
                return true;
            }
            catch (Exception ex)
            {
                m_logWriter.LogError(ex);
                return false;
            }
        }

        public bool ReadCoil(ushort address, ushort len, out bool[] data)
        {
            data = new bool[len];
            try
            {
                data = m_master.ReadCoils(1, address, len);
                return true;
            }
            catch (Exception ex)
            {
                m_logWriter.LogError(ex);
                return false;
            }
        }

        public bool WriteCoil(ushort address, bool data)
        {
            try
            {
                m_master.WriteSingleCoil(1, address, data);
                return true;
            }
            catch (Exception ex)
            {
                m_logWriter.LogError(ex);
                return false;
            }
        }

        public bool WriteCoil(ushort address, bool[] data)
        {
            try
            {
                m_master.WriteMultipleCoils(1, address, data);
                return true;
            }
            catch (Exception ex)
            {
                m_logWriter.LogError(ex);
                return false;
            }
        }

        public bool ReadHoldingRegister(ushort address, out ushort data)
        {
            data = 0;
            try
            {
                data = m_master.ReadHoldingRegisters(1, address, 1)[0];
                return true;
            }
            catch (Exception ex)
            {
                m_logWriter.LogError(ex);
                return false;
            }
        }

        public bool ReadHoldingRegister(ushort address, ushort len, out ushort[] data)
        {
            data = new ushort[len];
            try
            {
                data = m_master.ReadHoldingRegisters(1, address, len);
                return true;
            }
            catch (Exception ex)
            {
                m_logWriter.LogError(ex);
                return false;
            }
        }

        public bool WriteHoldingRegister(ushort address, ushort data)
        {
            try
            {
                m_master.WriteSingleRegister(1, address, data);
                return true;
            }
            catch (Exception ex)
            {
                m_logWriter.LogError(ex);
                return false;
            }
        }

        public bool WriteHoldingRegister(ushort address, ushort[] data)
        {
            try
            {
                m_master.WriteMultipleRegisters(1, address, data);
                return true;
            }
            catch (Exception ex)
            {
                m_logWriter.LogError(ex);
                return false;
            }
        }
    }
}