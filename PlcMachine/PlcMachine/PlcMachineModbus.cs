using ModbusInterface;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlcUtil.PlcMachine
{
    /// <summary>
    /// Modbus 대상으로 한 PlcMachine
    /// </summary>
    public class PlcMachineModbus : PlcMachine
    {
        private const string HOLDING_REGISTER = "H";
        private const string COIL = "C";

        private const int MAX_HOLDING_REGISTER_ADDRESS = 10000;
        private const int MAX_COIL_ADDRESS = 10000;
        private const ushort WORD_SCAN_SIZE = 125;
        private const ushort BIT_SCAN_SIZE = 2000;

        private Modbus m_modbus;

        private CancellationTokenSource m_cts = new CancellationTokenSource();

        public PlcMachineModbus(string ipAddress, int timeout = 5000) : this()
        {
            m_modbus = new Modbus(ipAddress);
            m_modbus.WriteTimeout = m_modbus.ReadTimeout = timeout;
        }

        private PlcMachineModbus()
        {
            _wordDataDict[HOLDING_REGISTER] = new WordData(MAX_HOLDING_REGISTER_ADDRESS);
            _bitDataDict[COIL] = new BitData(MAX_COIL_ADDRESS);
        }

        public override void CreateDevice()
        {
            CloseDevice();

            m_modbus.Start();
            m_cts = new CancellationTokenSource();
            Task.Run(() => ScanDevice(m_cts.Token));
        }

        protected override bool ScanBitData()
        {
            bool result = true;
            foreach (var key in _bitDataDict.Keys)
            {
                var addressList = m_scanAddressData.GetScanAddress(key);
                foreach (var address in addressList)
                {
                    if (!m_modbus.ReadCoil((ushort)address, BIT_SCAN_SIZE, out var data))
                        result = false;

                    _bitDataDict[key].SetData(address, data);
                }
            }
            return result;
        }

        protected override bool ScanWordData()
        {
            bool result = true;
            foreach (var key in _wordDataDict.Keys)
            {
                var addressList = m_scanAddressData.GetScanAddress(key);
                for (int i = 0; i < addressList.Count; i++)
                {
                    if (!m_modbus.ReadHoldingRegister((ushort)addressList[i], WORD_SCAN_SIZE, out var data))
                        result = false;

                    _wordDataDict[key].SetData(addressList[i], data);
                }
            }
            return result;
        }

        public override void CloseDevice()
        {
            m_cts.Cancel();
            m_modbus.Stop();

            foreach (var wordData in _wordDataDict.Values)
                wordData.ClearData();
            foreach (var bitData in _bitDataDict.Values)
                bitData.ClearData();
        }

        public override void GetBitData(string address, out bool value)
        {
            value = false;
            if (!ushort.TryParse(address, out var uAddress))
                return;

            if (!_bitDataDict.TryGetValue(COIL, out var bitData))
                return;
            m_scanAddressData.SetScanAddress(COIL, uAddress, 1, BIT_SCAN_SIZE);

            value = bitData.GetData(uAddress, 1)[0];
        }

        public override void SetBitData(string address, bool value)
        {
            if (!ushort.TryParse(address, out var uAddress))
                return;

            if (!_bitDataDict.TryGetValue(COIL, out var bitData))
                return;
            m_scanAddressData.SetScanAddress(COIL, uAddress, 1, BIT_SCAN_SIZE);

            if (m_modbus.WriteCoil(uAddress, value))
                bitData.SetData(uAddress, new bool[] { value });
        }

        public override void GetWordData(int address, int length, out string value)
        {
            value = string.Empty;
            if (!_wordDataDict.TryGetValue(HOLDING_REGISTER, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(HOLDING_REGISTER, address, length, WORD_SCAN_SIZE);

            ushort[] data = plcData.GetData(address, length);
            var sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                var bitData = BitConverter.GetBytes(data[i]);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(bitData);

                sb.Append(Encoding.ASCII.GetString(bitData));
            }
            value = sb.ToString();
            value = value.Trim('\0');
        }

        public override void GetWordData(int address, out short value)
        {
            value = 0;
            if (!_wordDataDict.TryGetValue(HOLDING_REGISTER, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(HOLDING_REGISTER, address, 1, WORD_SCAN_SIZE);

            ushort data = plcData.GetData(address, 1)[0];
            value = (short)data;
        }

        public override void GetWordData(int address, out int value)
        {
            value = 0;
            if (!_wordDataDict.TryGetValue(HOLDING_REGISTER, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(HOLDING_REGISTER, address, 2, WORD_SCAN_SIZE);

            ushort[] data = plcData.GetData(address, 2);
            value = (data[1] << 16) | data[0];
        }

        public override void SetWordData(int address, int length, string value)
        {
            if (!_wordDataDict.TryGetValue(HOLDING_REGISTER, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(HOLDING_REGISTER, address, length, WORD_SCAN_SIZE);

            if (value.Length % 2 != 0)
                value += '\0';

            while (value.Length < length * 2)
                value += "\0\0";

            ushort[] data = new ushort[length];
            for (int i = 0; i < length; i++)
                data[i] = (ushort)(value[1 + i * 2] << 8 | value[i * 2]);

            if (m_modbus.WriteHoldingRegister((ushort)address, data))
                plcData.SetData(address, data);
        }

        public override void SetWordData(int address, short value)
        {
            if (!_wordDataDict.TryGetValue(HOLDING_REGISTER, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(HOLDING_REGISTER, address, 1, WORD_SCAN_SIZE);

            ushort[] data = new ushort[] { (ushort)value };
            if (m_modbus.WriteHoldingRegister((ushort)address, data[0]))
                plcData.SetData(address, data);
        }

        public override void SetWordData(int address, int value)
        {
            if (!_wordDataDict.TryGetValue(HOLDING_REGISTER, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(HOLDING_REGISTER, address, 2, WORD_SCAN_SIZE);

            ushort[] data = new ushort[2];
            data[0] = (ushort)(value & 0xFFFF);
            data[1] = (ushort)((value >> 16) & 0xFFFF);
            if (m_modbus.WriteHoldingRegister((ushort)address, data))
                plcData.SetData(address, data);
        }
    }
}
