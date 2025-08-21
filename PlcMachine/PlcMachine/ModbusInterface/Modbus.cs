using NModbus;
using System;
using System.Threading;

namespace ModbusInterface
{
    public abstract class Modbus
    {
        protected ModbusLogWriter m_logWriter;

        protected IModbusMaster m_master;

        public int WriteTimeout = Timeout.Infinite;
        public int ReadTimeout = Timeout.Infinite;

        public abstract void Start();

        public abstract void Stop();

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

        public bool ReadInput(ushort address, out bool data)
        {
            data = false;
            try
            {
                data = m_master.ReadInputs(1, address, 1)[0];
                return true;
            }
            catch (Exception ex)
            {
                m_logWriter.LogError(ex);
                return false;
            }
        }

        public bool ReadInput(ushort address, ushort len, out bool[] data)
        {
            data = new bool[len];
            try
            {
                data = m_master.ReadInputs(1, address, len);
                return true;
            }
            catch (Exception ex)
            {
                m_logWriter.LogError(ex);
                return false;
            }
        }

        public bool ReadInputRegister(ushort address, out ushort data)
        {
            data = 0;
            try
            {
                data = m_master.ReadInputRegisters(1, address, 1)[0];
                return true;
            }
            catch (Exception ex)
            {
                m_logWriter.LogError(ex);
                return false;
            }
        }

        public bool ReadInputRegister(ushort address, ushort len, out ushort[] data)
        {
            data = new ushort[len];
            try
            {
                data = m_master.ReadInputRegisters(1, address, len);
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