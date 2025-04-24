using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UpperLinkInterface;

namespace YJPlcMachine
{
    /// <summary>
    /// Omron PLC 대상으로한 PlcMachine
    /// </summary>
    public class PlcMachineOmron : PlcMachine
    {
        private const string DM = "DM";

        private Upperlink m_upperLink;

        private CancellationTokenSource m_cts = new CancellationTokenSource();

        public PlcMachineOmron(string portName, int baudRate, Parity parity, int dataBit, StopBits stopBits, int timeout = 5000) : this()
        {
            m_upperLink = new Upperlink(portName, baudRate, parity, dataBit, stopBits);
            m_upperLink.WriteTimeout = m_upperLink.ReadTimeout = timeout;
        }

        private PlcMachineOmron()
        {
            m_plcAreaDict[DM] = new PlcData(MaxDataAreaAddress);
        }

        public override void CreateDevice()
        {
            CloseDevice();

            m_upperLink.Start();
            m_cts = new CancellationTokenSource();
            Task.Run(() => ScanTask(m_cts.Token));
        }

        public override void CloseDevice()
        {
            m_cts.Cancel();
            m_upperLink.Stop();

            foreach (var plcData in m_plcAreaDict.Values)
                plcData.ClearData();
        }

        protected async Task ScanTask(CancellationToken token)
        {
            bool isFirstLoop = true;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!isFirstLoop)
                        await Task.Delay(20);
                    isFirstLoop = false;

                    ScanData(DM);
                }
                finally
                {
                    OnDataUpdated?.Invoke();
                }
            }
        }

        private void ScanData(string code)
        {
            var addressList = m_scanAddressData.GetScanAddress(code);
            for (int i = 0; i < addressList.Count; i++)
            {
                m_upperLink.GetDMData(addressList[i], ScanAddressData.SCANSIZE, out var data);
                if (m_plcAreaDict.TryGetValue(code, out var plcData))
                    plcData.SetData(addressList[i], data);
            }
        }

        public override void GetContactArea(string address, out bool value)
        {
            value = false;
        }

        public override void SetContactArea(string address, bool value, bool waitUpdate = false)
        {
        }

        public override void GetDataArea(int address, int length, out string value)
        {
            value = string.Empty;
            if (!m_plcAreaDict.TryGetValue(DM, out var plcData))
                return;
            if (m_scanAddressData.SetScanAddress(DM, address, length))
                WaitScanFinish();

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
        }

        public override void GetDataArea(int address, out short value)
        {
            value = 0;
            if (!m_plcAreaDict.TryGetValue(DM, out var plcData))
                return;
            if (m_scanAddressData.SetScanAddress(DM, address, 1))
                WaitScanFinish();

            ushort data = plcData.GetData(address, 1)[0];
            value = (short)data;
        }

        public override void GetDataArea(int address, out int value)
        {
            value = 0;
            if (!m_plcAreaDict.TryGetValue(DM, out var plcData))
                return;
            if (m_scanAddressData.SetScanAddress(DM, address, 2))
                WaitScanFinish();

            ushort[] data = plcData.GetData(address, 2);
            value = (data[1] << 16) | data[0];
        }

        public override void SetDataArea(int address, int length, string value, bool waitUpdate = false)
        {
            if (!m_plcAreaDict.TryGetValue(DM, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(DM, address, length);

            if (value.Length % 2 != 0)
                value += '\0';

            while (value.Length < length * 2)
                value += "\0\0";

            ushort[] data = new ushort[length];
            for (int i = 0; i < length; i++)
                data[i] = (ushort)(value[1 + i * 2] << 8 | value[i * 2]);

            m_upperLink.SetDMData(address, length, data);

            if (waitUpdate)
                WaitScanFinish();
        }

        public override void SetDataArea(int address, short value, bool waitUpdate = false)
        {
            if (!m_plcAreaDict.TryGetValue(DM, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(DM, address, 1);

            ushort[] data = new ushort[] { (ushort)value };
            m_upperLink.SetDMData(address, 1, data);

            if (waitUpdate)
                WaitScanFinish();
        }

        public override void SetDataArea(int address, int value, bool waitUpdate = false)
        {
            if (!m_plcAreaDict.TryGetValue(DM, out var plcData))
                return;
            m_scanAddressData.SetScanAddress(DM, address, 2);

            ushort[] data = new ushort[2];
            data[0] = (ushort)(value & 0xFFFF);
            data[1] = (ushort)((value >> 16) & 0xFFFF);
            m_upperLink.SetDMData(address, 2, data);

            if (waitUpdate)
                WaitScanFinish();
        }
    }
}