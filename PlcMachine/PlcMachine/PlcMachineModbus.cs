using ModbusInterface;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlcUtil.PlcMachine
{
    /// <summary>
    /// Modbus 대상으로 한 PlcMachine
    /// </summary>
    public abstract class PlcMachineModbus : PlcMachine
    {
        private const string COIL = "1";
        private const string DISCRETE_INPUT = "2";
        private const string INPUT_REGISTER = "3";
        private const string HOLDING_REGISTER = "4";

        private const int MAX_MODBUS_ADDRESS = 10000;
        private const ushort WORD_SCAN_SIZE = 100;
        private const ushort BIT_SCAN_SIZE = 2000;

        protected Modbus m_modbus;

        private CancellationTokenSource m_cts = new CancellationTokenSource();

        protected PlcMachineModbus()
        {
            _wordDataDict[HOLDING_REGISTER] = new WordData(MAX_MODBUS_ADDRESS);
            _bitDataDict[COIL] = new BitData(MAX_MODBUS_ADDRESS);
        }

        public override void CreateDevice()
        {
            CloseDevice();

            m_modbus.Start();
            m_cts = new CancellationTokenSource();
            Task.Run(() => ScanDevice(m_cts.Token));
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

        protected override bool ScanBitData()
        {
            bool result = true;
            foreach (var key in _bitDataDict.Keys)
            {
                var addressList = m_scanAddressData.GetScanAddress(key);
                foreach (var address in addressList)
                {
                    var data = new bool[BIT_SCAN_SIZE];
                    if (key == COIL && !m_modbus.ReadCoil((ushort)address, BIT_SCAN_SIZE, out data))
                        result = false;
                    else if (key == DISCRETE_INPUT && !m_modbus.ReadInput((ushort)address, BIT_SCAN_SIZE, out data))
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
                foreach (var address in addressList)
                {
                    var data = new ushort[WORD_SCAN_SIZE];
                    if (key == INPUT_REGISTER && !m_modbus.ReadInputRegister((ushort)address, WORD_SCAN_SIZE, out data))
                        result = false;
                    if (key == HOLDING_REGISTER && !m_modbus.ReadHoldingRegister((ushort)address, WORD_SCAN_SIZE, out data))
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
            if (m_scanAddressData.SetScanAddress(key, index, 1, BIT_SCAN_SIZE))
                WaitScanComplete();

            return _bitDataDict[key].GetData(index, 1)[0];
        }

        public override void SetBitData(string address, bool value)
        {
            if (!GetBitAddress(address, out var key, out var index))
                return;
            m_scanAddressData.SetScanAddress(key, index, 1, BIT_SCAN_SIZE);

            if (key == COIL && m_modbus.WriteCoil(index, value))
                _bitDataDict[key].SetData(index, new bool[] { value });
        }

        public override string GetWordDataASCII(string address, int length)
        {
            if (!GetWordAddress(address, out string key, out int index))
                return string.Empty;
            if (m_scanAddressData.SetScanAddress(key, index, length, WORD_SCAN_SIZE))
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
            if (m_scanAddressData.SetScanAddress(key, index, 1, WORD_SCAN_SIZE))
                WaitScanComplete();

            ushort data = _wordDataDict[key].GetData(index, 1)[0];
            return (short)data;
        }

        public override int GetWordDataInt(string address)
        {
            if (!GetWordAddress(address, out string key, out int index))
                return 0;
            if (m_scanAddressData.SetScanAddress(key, index, 2, WORD_SCAN_SIZE))
                WaitScanComplete();

            ushort[] data = _wordDataDict[key].GetData(index, 2);
            return (data[1] << 16) | data[0];
        }

        public override void SetWordDataASCII(string address, int length, string value)
        {
            if (!GetWordAddress(address, out string key, out int index))
                return;
            if (m_scanAddressData.SetScanAddress(key, index, 2, WORD_SCAN_SIZE))
                WaitScanComplete();

            if (value.Length % 2 != 0)
                value += '\0';

            while (value.Length < length * 2)
                value += "\0\0";

            ushort[] data = new ushort[length];
            for (int i = 0; i < length; i++)
                data[i] = (ushort)(value[1 + i * 2] << 8 | value[i * 2]);

            if (key == HOLDING_REGISTER && m_modbus.WriteHoldingRegister((ushort)index, data))
                _wordDataDict[key].SetData(index, data);
        }

        public override void SetWordDataShort(string address, short value)
        {
            if (!GetWordAddress(address, out string key, out int index))
                return;
            if (m_scanAddressData.SetScanAddress(key, index, 2, WORD_SCAN_SIZE))
                WaitScanComplete();

            ushort[] data = new ushort[] { (ushort)value };
            if (key == HOLDING_REGISTER && m_modbus.WriteHoldingRegister((ushort)index, data[0]))
                _wordDataDict[key].SetData(index, data);
        }

        public override void SetWordDataInt(string address, int value)
        {
            if (!GetWordAddress(address, out string key, out int index))
                return;
            if (m_scanAddressData.SetScanAddress(key, index, 2, WORD_SCAN_SIZE))
                WaitScanComplete();

            ushort[] data = new ushort[2];
            data[0] = (ushort)(value & 0xFFFF);
            data[1] = (ushort)((value >> 16) & 0xFFFF);
            if (key == HOLDING_REGISTER && m_modbus.WriteHoldingRegister((ushort)index, data))
                _wordDataDict[key].SetData(index, data);
        }

        protected bool GetBitAddress(string address, out string key, out ushort index)
        {
            key = string.Empty;
            index = 0;
            address = address.ToUpper();
            var bitKeys = new HashSet<string> { COIL, DISCRETE_INPUT };

            foreach (var bitKey in bitKeys)
            {
                if (!address.StartsWith(bitKey) || address.Length < bitKey.Length + 1)
                    continue;

                var sAddress = address.Substring(bitKey.Length);
                if (ushort.TryParse(sAddress, out var bitAddress))
                {
                    key = bitKey;
                    index = (ushort)(bitAddress - 1);
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
            var bitKeys = new HashSet<string> { INPUT_REGISTER, HOLDING_REGISTER };

            foreach (var bitKey in bitKeys)
            {
                if (!address.StartsWith(bitKey) || address.Length < bitKey.Length + 1)
                    continue;

                var sAddress = address.Substring(bitKey.Length);
                if (int.TryParse(sAddress, out var wordAddress))
                {
                    key = bitKey;
                    index = (ushort)(wordAddress);
                    return true;
                }
            }
            return false;
        }
    }
}
