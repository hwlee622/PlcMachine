using ModbusInterface;

namespace PlcUtil.PlcMachine
{
    /// <summary>
    /// Modbus Udp 통신 PlcMachine
    /// </summary>
    public class PlcMachineModbusUdp : PlcMachineModbus
    {
        public PlcMachineModbusUdp(string ipAddress, int port, int timeout = 5000) : base()
        {
            m_modbus = new ModbusUdp(ipAddress, port);
            m_modbus.WriteTimeout = m_modbus.ReadTimeout = timeout;
        }
    }
}
