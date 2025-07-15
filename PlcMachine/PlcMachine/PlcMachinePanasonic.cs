using MewtocolInterface;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlcUtil.PlcMachine
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
        private const int MAX_CONTACT_ADDRESS = 16000;
        private const int SCAN_SIZE = 250;

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
                    if (!m_mewtocol.GetDIOData(key, address, SCAN_SIZE, out ushort[] data))
                        result = false;

                    bool[] bitData = new bool[data.Length * 16];
                    for (int i = 0; i < data.Length; i++)
                        for (int j = 0; j < 16; j++)
                            bitData[i * 16 + j] = ((data[i] >> j) & 1) == 1;

                    _bitDataDict[key].SetData(address * 16, bitData);
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
                    if (!m_mewtocol.GetDTData(address, SCAN_SIZE, out ushort[] data))
                        result = false;

                    _wordDataDict[key].SetData(address, data);
                }
            }
            return result;
        }

        public override bool GetBitData(string address)
        {
            if (!GetBitAddress(address, out var key, out var index))
                return false;
            if (m_scanAddressData.SetScanAddress(key, index / 16, 1, SCAN_SIZE))
                WaitScanComplete();

            return _bitDataDict[key].GetData(index, 1)[0];
        }

        public override void SetBitData(string address, bool value)
        {
            if (!GetBitAddress(address, out var key, out var index))
                return;
            m_scanAddressData.SetScanAddress(key, index / 16, 1, SCAN_SIZE);

            if (key != X && m_mewtocol.SetDIOData(key, index / 16, index % 16, value))
                _bitDataDict[key].SetData(index, new bool[] { value });
        }

        public override string GetWordDataASCII(string address, int length)
        {
            if (!GetWordAddress(address, out string key, out int index))
                return string.Empty;
            if (m_scanAddressData.SetScanAddress(key, index, length, SCAN_SIZE))
                WaitScanComplete();

            ushort[] data = _wordDataDict[key].GetData(index, length);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                byte[] bitData = BitConverter.GetBytes(data[i]);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(bitData);

                sb.Append(Encoding.ASCII.GetString(bitData));
            }
            return sb.ToString().Trim('\0');
        }

        public override short GetWordDataShort(string address)
        {
            if (!GetWordAddress(address, out string key, out int index))
                return 0;
            if (m_scanAddressData.SetScanAddress(key, index, 1, SCAN_SIZE))
                WaitScanComplete();

            ushort data = _wordDataDict[key].GetData(index, 1)[0];
            return (short)data;
        }

        public override int GetWordDataInt(string address)
        {
            if (!GetWordAddress(address, out string key, out int index))
                return 0;
            if (m_scanAddressData.SetScanAddress(key, index, 2, SCAN_SIZE))
                WaitScanComplete();

            ushort[] data = _wordDataDict[key].GetData(index, 2);
            return (data[1] << 16) | data[0];
        }

        public override void SetWordDataASCII(string address, int length, string value)
        {
            if (!GetWordAddress(address, out string key, out int index))
                return;
            if (m_scanAddressData.SetScanAddress(key, index, 2, SCAN_SIZE))
                WaitScanComplete();

            if (value.Length % 2 != 0)
                value += '\0';

            while (value.Length < length * 2)
                value += "\0\0";

            ushort[] data = new ushort[length];
            for (int i = 0; i < length; i++)
                data[i] = (ushort)(value[1 + i * 2] << 8 | value[i * 2]);

            if (m_mewtocol.SetDTData(index, length, data))
                _wordDataDict[key].SetData(index, data);
        }

        public override void SetWordDataShort(string address, short value)
        {
            if (!GetWordAddress(address, out string key, out int index))
                return;
            if (m_scanAddressData.SetScanAddress(key, index, 2, SCAN_SIZE))
                WaitScanComplete();

            ushort[] data = new ushort[] { (ushort)value };
            if (m_mewtocol.SetDTData(index, 1, data))
                _wordDataDict[key].SetData(index, data);
        }

        public override void SetWordDataInt(string address, int value)
        {
            if (!GetWordAddress(address, out string key, out int index))
                return;
            if (m_scanAddressData.SetScanAddress(key, index, 2, SCAN_SIZE))
                WaitScanComplete();

            ushort[] data = new ushort[2];
            data[0] = (ushort)(value & 0xFFFF);
            data[1] = (ushort)((value >> 16) & 0xFFFF);
            if (m_mewtocol.SetDTData(index, 2, data))
                _wordDataDict[key].SetData(index, data);
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

        protected bool GetBitAddress(string address, out string key, out int index)
        {
            key = string.Empty;
            index = 0;
            address = address.ToUpper();
            var bitKeys = new HashSet<string>() { R, X, Y };

            foreach (var bitKey in bitKeys)
            {
                if (!address.StartsWith(bitKey) || address.Length < bitKey.Length + 2)
                    continue;

                var sBitAddress = address.Substring(bitKey.Length, address.Length - bitKey.Length - 1);
                var sHex = address.Substring(address.Length - 1, 1);
                if (int.TryParse(sBitAddress, out var bitAddress) && TryParseHexToInt(sHex, out var hex))
                {
                    key = bitKey;
                    index = bitAddress * 16 + hex;
                    return true;
                }
            }
            return false;
        }

        protected bool GetWordAddress(string address, out string key, out int index)
        {
            key = string.Empty;
            index = 0;
            address = address.ToUpper();
            var bitKeys = new HashSet<string> { DT };

            foreach(var bitKey in bitKeys)
            {
                if (!address.StartsWith(bitKey) || address.Length < bitKey.Length + 1)
                    continue;

                var sAddress = address.Substring(bitKey.Length);
                if (int.TryParse(sAddress, out var wordAddress))
                {
                    key = bitKey;
                    index = wordAddress;
                    return true;
                }
            }
            return false;
        }
    }
}