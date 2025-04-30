using System;
using System.IO.Ports;
using System.Text;
using CommInterface;

namespace UpperLinkInterface
{
    public class Upperlink
    {
        private UpperlinkLogWriter m_logWriter;
        private Comm m_comm;

        public int WriteTimeout
        {
            get { return m_comm.WriteTimeout; }
            set { m_comm.WriteTimeout = value; }
        }

        public int ReadTimeout
        {
            get { return m_comm.ReadTimeout; }
            set { m_comm.ReadTimeout = value; }
        }

        public Upperlink(string portNumber, int baudRate, Parity parity, int dataBit, StopBits stopBits)
        {
            m_logWriter = new UpperlinkLogWriter(portNumber);

            m_comm = new CommInterfaceSerial(portNumber, baudRate, parity, dataBit, stopBits);
            m_comm.SetSTX(Encoding.ASCII.GetBytes(new char[] { '@' }));
            m_comm.SetETX(Encoding.ASCII.GetBytes(new char[] { '*', (char)0x0D }));
            m_comm.OnError += ex => m_logWriter.LogError(ex);
        }

        public void Start()
        {
            m_comm?.Start();
        }

        public void Stop()
        {
            m_comm?.Stop();
        }

        /// <summary>
        /// DM Area Read
        /// </summary>
        /// <param name="address"></param>
        /// <param name="size"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool GetDMData(int address, int size, out ushort[] data)
        {
            data = new ushort[size];

            string command = $"@01RD{address:0000}{size:0000}";
            command = GetFCSMessage(command);

            byte[] sendBytes = Encoding.ASCII.GetBytes(command);
            if (GetReply(sendBytes, out byte[] recvBytes))
            {
                string reply = Encoding.ASCII.GetString(recvBytes);
                int length = Math.Min((reply.Length - 11) / 4, size);
                for (int i = 0; i < length; i++)
                {
                    int index = 7 + i * 4;
                    string sValue = reply.Substring(index, 4);
                    ushort value = Convert.ToUInt16(sValue, 16);
                    data[i] = value;
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// DM Area Write
        /// </summary>
        /// <param name="address"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool SetDMData(int address, int size, ushort[] data)
        {
            size = Math.Min(size, data.Length);

            StringBuilder sb = new StringBuilder();
            sb.Append($"@01WD{address:0000}");
            for (int i = 0; i < size; i++)
            {
                byte[] bitData = BitConverter.GetBytes(data[i]);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bitData);
                sb.Append($"{bitData[0]:X2}{bitData[1]:X2}");
            }

            string command = sb.ToString();
            command = GetFCSMessage(command);

            byte[] sendBytes = Encoding.ASCII.GetBytes(command);
            return GetReply(sendBytes, out byte[] recvBytes);
        }

        private bool GetReply(byte[] sendBytes, out byte[] recvBytes)
        {
            recvBytes = m_comm.SendReceiveMessage(sendBytes);
            if (CheckFCSError(recvBytes) || CheckReplyError(recvBytes))
            {
                m_logWriter.Log($"SendRecv Error.\r\n{Encoding.ASCII.GetString(sendBytes)}\r\n{Encoding.ASCII.GetString(recvBytes)}");
                return false;
            }
            else
                return true;
        }

        private string GetFCSMessage(string message)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(message);
            byte fcs = 0;
            foreach (byte b in bytes)
                fcs ^= b;

            return $"{message}{fcs:X2}*{(char)0x0D}";
        }

        private bool CheckFCSError(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 5)
                return true;

            string message = Encoding.ASCII.GetString(bytes);
            byte fcs = 0;
            for (int i = 0; i < bytes.Length - 4; i++)
                fcs ^= bytes[i];

            if (!string.Equals(message.Substring(message.Length - 4, 2), $"{fcs:X2}"))
                return true;
            else
                return false;
        }

        private bool CheckReplyError(byte[] recvBytes)
        {
            if (recvBytes == null || recvBytes.Length < 9)
                return true;
            else if (recvBytes[3] == (byte)'I' && recvBytes[4] == (byte)'C')
                return true;
            else if (recvBytes[5] != (byte)'0' || recvBytes[6] != (byte)'0')
                return true;
            else
                return false;
        }
    }
}