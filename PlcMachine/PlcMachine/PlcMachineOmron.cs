using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UpperLinkInterface;

namespace PlcUtil.PlcMachine
{
    /// <summary>
    /// Omron PLC 대상으로한 PlcMachine
    /// </summary>
    public class PlcMachineOmron : PlcMachine
    {
        private const string DM = "DM";
        private const int MAX_DM_ADDRESS = 50000;
        private const int SCAN_SIZE = 250;

        private Upperlink m_upperLink;

        private CancellationTokenSource m_cts = new CancellationTokenSource();

        public PlcMachineOmron(string portName, int baudRate, Parity parity, int dataBit, StopBits stopBits, int timeout = 5000) : this()
        {
            m_upperLink = new Upperlink(portName, baudRate, parity, dataBit, stopBits);
            m_upperLink.WriteTimeout = m_upperLink.ReadTimeout = timeout;
        }

        private PlcMachineOmron()
        {
            _wordDataDict[DM] = new WordData(MAX_DM_ADDRESS);
        }

        public override void CreateDevice()
        {
            CloseDevice();

            m_upperLink.Start();
            m_cts = new CancellationTokenSource();
            Task.Run(() => ScanDevice(m_cts.Token));
        }

        public override void CloseDevice()
        {
            m_cts.Cancel();
            m_upperLink.Stop();

            foreach (var bitData in _bitDataDict.Values)
                bitData.ClearData();
            foreach (var wordData in _wordDataDict.Values)
                wordData.ClearData();
        }

        protected override bool ScanBitData()
        {
            return true;
        }

        protected override bool ScanWordData()
        {
            bool result = true;
            foreach (var key in _wordDataDict.Keys)
            {
                var addressList = m_scanAddressData.GetScanAddress(key);
                foreach (var address in addressList)
                {
                    if (!m_upperLink.GetDMData(address, SCAN_SIZE, out ushort[] data))
                        result = false;

                    _wordDataDict[key].SetData(address, data);
                }
            }
            return result;
        }

        public override bool GetBitData(string address)
        {
            return false;
        }

        public override void SetBitData(string address, bool value)
        {
        }

        public override string GetWordDataASCII(int address, int length)
        {
            if (!_wordDataDict.TryGetValue(DM, out var wordData))
                return string.Empty;
            if (m_scanAddressData.SetScanAddress(DM, address, length, SCAN_SIZE))
                WaitScanComplete();

            ushort[] data = wordData.GetData(address, length);
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

        public override short GetWordDataShort(int address)
        {
            if (!_wordDataDict.TryGetValue(DM, out var wordData))
                return 0;
            if (m_scanAddressData.SetScanAddress(DM, address, 1, SCAN_SIZE))
                WaitScanComplete();

            ushort data = wordData.GetData(address, 1)[0];
            return (short)data;
        }

        public override int GetWordDataInt(int address)
        {
            if (!_wordDataDict.TryGetValue(DM, out var wordData))
                return 0;
            if (m_scanAddressData.SetScanAddress(DM, address, 2, SCAN_SIZE))
                WaitScanComplete();

            ushort[] data = wordData.GetData(address, 2);
            return (data[1] << 16) | data[0];
        }

        public override void SetWordDataASCII(int address, int length, string value)
        {
            if (!_wordDataDict.TryGetValue(DM, out var wordData))
                return;
            m_scanAddressData.SetScanAddress(DM, address, length, SCAN_SIZE);

            if (value.Length % 2 != 0)
                value += '\0';

            while (value.Length < length * 2)
                value += "\0\0";

            ushort[] data = new ushort[length];
            for (int i = 0; i < length; i++)
                data[i] = (ushort)(value[1 + i * 2] << 8 | value[i * 2]);

            if (m_upperLink.SetDMData(address, length, data))
                wordData.SetData(address, data);
        }

        public override void SetWordDataShort(int address, short value)
        {
            if (!_wordDataDict.TryGetValue(DM, out var wordData))
                return;
            m_scanAddressData.SetScanAddress(DM, address, 1, SCAN_SIZE);

            ushort[] data = new ushort[] { (ushort)value };
            if (m_upperLink.SetDMData(address, 1, data))
                wordData.SetData(address, data);
        }

        public override void SetWordDataInt(int address, int value)
        {
            if (!_wordDataDict.TryGetValue(DM, out var wordData))
                return;
            m_scanAddressData.SetScanAddress(DM, address, 2, SCAN_SIZE);

            ushort[] data = new ushort[2];
            data[0] = (ushort)(value & 0xFFFF);
            data[1] = (ushort)((value >> 16) & 0xFFFF);
            if (m_upperLink.SetDMData(address, 2, data))
                wordData.SetData(address, data);
        }
    }
}