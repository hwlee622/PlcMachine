using ModbusInterface;

namespace PlcUtil.PlcMachine
{
    /// <summary>
    /// Modbus Tcp 통신 PlcMachine
    /// </summary>
    public class PlcMachineModbusTcp : PlcMachineModbus
    {
        public PlcMachineModbusTcp(string ipAddress, int port, int timeout = 5000) : base()
        {
            m_modbus = new ModbusTcp(ipAddress, port);
            m_modbus.WriteTimeout = m_modbus.ReadTimeout = timeout;
        }
    }
}
