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
            m_plcAreaDict[DT] = new PlcData(MaxDataAreaAddress);
            m_plcAreaDict[R] = new PlcData(MaxContactAddress);
            m_plcAreaDict[Y] = new PlcData(MaxContactAddress);
            m_plcAreaDict[X] = new PlcData(MaxContactAddress);
        }

        public override void CreateDevice()
        {
            CloseDevice();

            m_mewtocol.Start();
            m_cts = new CancellationTokenSource();
            Task.Run(() => ScanTask(m_cts.Token));
        }

        public override void CloseDevice()
        {
            m_cts.Cancel();
            m_mewtocol.Stop();

            foreach (var plcData in m_plcAreaDict.Values)
                plcData.ClearData();
        }

        protected async Task ScanTask(CancellationToken token)
        {
            bool isFirstLoop = true;
            int loopTick = 0;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!isFirstLoop)
                        await Task.Delay(20);
                    isFirstLoop = false;

                    loopTick = (loopTick + 1) % 10;
                    if (loopTick == 0)
                        m_scanAddressData.ExpireOldScanAddress(TimeSpan.FromMinutes(10));

                    bool dtScanResult = ScanData(DT);
                    bool rScanResult = ScanContact(R);
                    bool yScanResult = ScanContact(Y);
                    bool xScanResult = ScanContact(X);

                    IsConnected = dtScanResult && rScanResult && yScanResult && xScanResult;
                }
                finally
                {
                    OnDataUpdated?.Invoke();
                }
            }
        }

        private bool ScanData(string code)
        {
            bool result = true;
            var addressList = m_scanAddressData.GetScanAddress(code);
            for (int i = 0; i < addressList.Count; i++)
            {
                bool scanResult = m_mewtocol.GetDTData(addressList[i], ScanAddressData.SCANSIZE, out ushort[] data);
                if (!scanResult)
                    IsConnected = result = false;

                if (m_plcAreaDict.TryGetValue(code, out var plcData))
                    plcData.SetData(addressList[i], data);
            }
            return result;
        }

        private bool ScanContact(string code)
        {
            bool result = true;
            var addressList = m_scanAddressData.GetScanAddress(code);
            for (int i = 0; i < addressList.Count; i++)
            {
                bool scanResult = m_mewtocol.GetDIOData(code, addressList[i], ScanAddressData.SCANSIZE, out ushort[] data);
                if (!scanResult)
                    IsConnected = result = false;

                if (m_plcAreaDict.TryGetValue(code, out var plcData))
                    plcData.SetData(addressList[i], data);
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

            if (!m_plcAreaDict.TryGetValue(contactCode, out var plcData) || !int.TryParse(sContactAddress, out int contactAddress) || !TryParseHexToInt(sHex, out int hex))
                return;
            m_scanAddressData.SetScanAddress(contactCode, contactAddress, 1);

            ushort data = plcData.GetData(contactAddress, 1)[0];
            value = ((data >> hex) & 1) == 1;
        }

        public override void SetContactArea(string address, bool value)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 3)
                return;

            string contactCode = address.Substring(0, 1).ToUpper();
            string sContactAddress = address.Substring(1, address.Length - 2);
            string sHex = address.Substring(address.Length - 1, 1).ToUpper();

            if (!m_plcAreaDict.TryGetValue(contactCode, out var plcData) || !int.TryParse(sContactAddress, out int contactAddress) || !TryParseHexToInt(sHex, out int hex))
                return;
            m_scanAddressData.SetScanAddress(contactCode, contactAddress, 1);

            if (m_mewtocol.SetDIOData(contactCode, contactAddress, hex, value))
            {
                int mask = 1 << hex;
                ushort[] data = plcData.GetData(contactAddress, 1);
                data[0] = value ? (ushort)(data[0] | mask) : (ushort)(data[0] & ~mask);
                plcData.SetData(contactAddress, data);
            }
        }

        public override void GetDataArea(int address, int length, out string value)
        {
            value = string.Empty;
            if (!m_plcAreaDict.TryGetValue(DT, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(DT, address, length);

            ushort[] data = plcData.GetData(address, length);
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
            if (!m_plcAreaDict.TryGetValue(DT, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(DT, address, 1);

            ushort data = plcData.GetData(address, 1)[0];
            value = (short)data;
        }

        public override void GetDataArea(int address, out int value)
        {
            value = 0;
            if (!m_plcAreaDict.TryGetValue(DT, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(DT, address, 2);

            ushort[] data = plcData.GetData(address, 2);
            value = (data[1] << 16) | data[0];
        }

        public override void SetDataArea(int address, int length, string value)
        {
            if (!m_plcAreaDict.TryGetValue(DT, out var plcData))
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
                plcData.SetData(address, data);
        }

        public override void SetDataArea(int address, short value)
        {
            if (!m_plcAreaDict.TryGetValue(DT, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(DT, address, 1);

            ushort[] data = new ushort[] { (ushort)value };
            if (m_mewtocol.SetDTData(address, 1, data))
                plcData.SetData(address, data);
        }

        public override void SetDataArea(int address, int value)
        {
            if (!m_plcAreaDict.TryGetValue(DT, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(DT, address, 2);

            ushort[] data = new ushort[2];
            data[0] = (ushort)(value & 0xFFFF);
            data[1] = (ushort)((value >> 16) & 0xFFFF);
            if (m_mewtocol.SetDTData(address, 2, data))
                plcData.SetData(address, data);
        }
    }
}