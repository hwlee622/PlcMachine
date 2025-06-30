using MewtocolInterface;
using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlcMachine
{
    /// <summary>
    /// 파나소닉 PLC 대상으로 한 PlcMachine
    /// </summary>
    public class PlcMachinePanasonic : PlcMachine
    {
        private const string DT = "DT";
        private const string R = "R";
        private const string Y = "Y";
        private const string X = "X";

        private const int MAX_DT_ADDRESS = 50000;
        private const int MAX_CONTACT_ADDRESS = 1000;

        private Mewtocol m_mewtocol;

        private CancellationTokenSource m_cts = new CancellationTokenSource();

        public PlcMachinePanasonic(string ipAddress, int port, int timeout = 5000) : this()
        {
            m_mewtocol = new Mewtocol(ipAddress, port);
            m_mewtocol.WriteTimeout = m_mewtocol.ReadTimeout = timeout;
        }

        public PlcMachinePanasonic(string portName, int baudRate, Parity parity, int dataBit, StopBits stopBits, int timeout = 5000) : this()
        {
            m_mewtocol = new Mewtocol(portName, baudRate, parity, dataBit, stopBits);
            m_mewtocol.WriteTimeout = m_mewtocol.ReadTimeout = timeout;
        }

        private PlcMachinePanasonic()
        {
            _wordDataDict[DT] = new WordData(MAX_DT_ADDRESS);
            _bitDataDict[R] = new BitData(MAX_CONTACT_ADDRESS);
            _bitDataDict[Y] = new BitData(MAX_CONTACT_ADDRESS);
            _bitDataDict[X] = new BitData(MAX_CONTACT_ADDRESS);
        }

        public override void CreateDevice()
        {
            CloseDevice();

            m_mewtocol.Start();
            m_cts = new CancellationTokenSource();
            Task.Run(() => ScanDevice(m_cts.Token));
        }

        public override void CloseDevice()
        {
            m_cts.Cancel();
            m_mewtocol.Stop();

            foreach (var bitData in _bitDataDict.Values)
                bitData.ClearData();
            foreach (var wordData in _wordDataDict.Values)
                wordData.ClearData();
        }

        protected override bool ScanBitData()
        {
            bool result = true;
            foreach (var key in _bitDataDict.Keys)
            {
                var addressList = m_scanAddressData.GetScanAddress(key);
                foreach (var address in addressList)
                {
                    if (!m_mewtocol.GetDIOData(key, address, ScanAddressData.SCANSIZE, out ushort[] data))
                        result = false;

                    bool[] bitData = new bool[data.Length * 16];
                    for (int i = 0; i < data.Length; i++)
                        for (int j = 0; j < 16; j++)
                            bitData[i * 16 + j] = ((data[i] >> j) & 1) == 1;

                    _bitDataDict[key].SetData(address, bitData);
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
                foreach (var address in addressList)
                {
                    if (!m_mewtocol.GetDTData(address, ScanAddressData.SCANSIZE, out ushort[] data))
                        result = false;

                    _wordDataDict[key].SetData(address, data);
                }
            }
            return result;
        }

        public override void GetContactArea(string address, out bool value)
        {
            value = false;
            if (string.IsNullOrEmpty(address) || address.Length < 3)
                return;

            string contactCode = address.Substring(0, 1).ToUpper();
            string sContactAddress = address.Substring(1, address.Length - 2);
            string sHex = address.Substring(address.Length - 1, 1).ToUpper();

            if (!_bitDataDict.TryGetValue(contactCode, out var bitData) || !int.TryParse(sContactAddress, out int contactAddress) || !TryParseHexToInt(sHex, out int hex))
                return;
            m_scanAddressData.SetScanAddress(contactCode, contactAddress, 1);

            value = bitData.GetData(contactAddress * 16 + hex, 1)[0];
        }

        public override void SetContactArea(string address, bool value)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 3)
                return;

            string contactCode = address.Substring(0, 1).ToUpper();
            string sContactAddress = address.Substring(1, address.Length - 2);
            string sHex = address.Substring(address.Length - 1, 1).ToUpper();

            if (!_bitDataDict.TryGetValue(contactCode, out var bitData) || !int.TryParse(sContactAddress, out int contactAddress) || !TryParseHexToInt(sHex, out int hex))
                return;
            m_scanAddressData.SetScanAddress(contactCode, contactAddress, 1);

            if (m_mewtocol.SetDIOData(contactCode, contactAddress, hex, value))
                bitData.SetData(contactAddress * 16 + hex, new bool[] { value });
        }

        public override void GetDataArea(int address, int length, out string value)
        {
            value = string.Empty;
            if (!_wordDataDict.TryGetValue(DT, out var wordData))
                return;
            m_scanAddressData.SetScanAddress(DT, address, length);

            ushort[] data = wordData.GetData(address, length);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                byte[] bitData = BitConverter.GetBytes(data[i]);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(bitData);

                sb.Append(Encoding.ASCII.GetString(bitData));
            }
            value = sb.ToString();
            value = value.Trim('\0');
        }

        public override void GetDataArea(int address, out short value)
        {
            value = 0;
            if (!_wordDataDict.TryGetValue(DT, out var wordData))
                return;
            m_scanAddressData.SetScanAddress(DT, address, 1);

            ushort data = wordData.GetData(address, 1)[0];
            value = (short)data;
        }

        public override void GetDataArea(int address, out int value)
        {
            value = 0;
            if (!_wordDataDict.TryGetValue(DT, out var wordData))
                return;
            m_scanAddressData.SetScanAddress(DT, address, 2);

            ushort[] data = wordData.GetData(address, 2);
            value = (data[1] << 16) | data[0];
        }

        public override void SetDataArea(int address, int length, string value)
        {
            if (!_wordDataDict.TryGetValue(DT, out var wordData))
                return;
            m_scanAddressData.SetScanAddress(DT, address, length);

            if (value.Length % 2 != 0)
                value += '\0';

            while (value.Length < length * 2)
                value += "\0\0";

            ushort[] data = new ushort[length];
            for (int i = 0; i < length; i++)
                data[i] = (ushort)(value[1 + i * 2] << 8 | value[i * 2]);

            if (m_mewtocol.SetDTData(address, length, data))
                wordData.SetData(address, data);
        }

        public override void SetDataArea(int address, short value)
        {
            if (!_wordDataDict.TryGetValue(DT, out var wordData))
                return;
            m_scanAddressData.SetScanAddress(DT, address, 1);

            ushort[] data = new ushort[] { (ushort)value };
            if (m_mewtocol.SetDTData(address, 1, data))
                wordData.SetData(address, data);
        }

        public override void SetDataArea(int address, int value)
        {
            if (!_wordDataDict.TryGetValue(DT, out var wordData))
                return;
            m_scanAddressData.SetScanAddress(DT, address, 2);

            ushort[] data = new ushort[2];
            data[0] = (ushort)(value & 0xFFFF);
            data[1] = (ushort)((value >> 16) & 0xFFFF);
            if (m_mewtocol.SetDTData(address, 2, data))
                wordData.SetData(address, data);
        }

        protected bool TryParseHexToInt(string hex, out int value)
        {
            value = 0;
            try
            {
                value = Convert.ToInt16(hex, 16);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}