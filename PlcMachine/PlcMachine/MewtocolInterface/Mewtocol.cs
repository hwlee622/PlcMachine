using System;
using System.IO.Ports;
using System.Text;
using YJComm;

namespace MewtocolInterface
{
    public class Mewtocol
    {
        private MewtocolLogWriter m_logWriter;
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

        public Mewtocol(string ipAddress, int port)
        {
            m_logWriter = new MewtocolLogWriter(ipAddress, port);

            m_comm = new CommUdp(ipAddress, port);
            m_comm.SetSTX(Encoding.ASCII.GetBytes(new char[] { '<' }));
            m_comm.SetETX(Encoding.ASCII.GetBytes(new char[] { (char)0x0D }));
            m_comm.OnError += ex => m_logWriter.LogError(ex);
        }

        public Mewtocol(string portNumber, int baudRate, Parity parity, int dataBit, StopBits stopBits)
        {
            m_logWriter = new MewtocolLogWriter(portNumber);

            m_comm = new CommSerial(portNumber, baudRate, parity, dataBit, stopBits);
            m_comm.SetSTX(Encoding.ASCII.GetBytes(new char[] { '<' }));
            m_comm.SetETX(Encoding.ASCII.GetBytes(new char[] { (char)0x0D }));
            m_comm.OnError += ex => m_logWriter.LogError(ex);
            m_comm.OnSendLog += bytes => m_logWriter.Log($"Send : {Encoding.ASCII.GetString(bytes)}");
            m_comm.OnReceiveLog += bytes => m_logWriter.Log($"Recv : {Encoding.ASCII.GetString(bytes)}");
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
        /// 접점 에리어 리드(단점)
        /// </summary>
        /// <param name="contactCode"></param>
        /// <param name="address"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool GetDIOData(string contactCode, int address, int hex, out bool data)
        {
            data = false;

            string command = $"<01#RCS{contactCode}{address:D3}{hex:X}";
            command = GetBCCMessage(command);

            byte[] sendBytes = Encoding.ASCII.GetBytes(command);
            if (GetReply(sendBytes, out byte[] recvBytes))
            {
                string reply = Encoding.ASCII.GetString(recvBytes);
                if (reply.Length >= 7)
                {
                    int value = reply[6] - '0';
                    data = value > 0;
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// 접점 에리어 리드(복수점)
        /// </summary>
        /// <returns></returns>
        public bool GetDIOData(string[] contactCode, int[] address, int[] hex, out bool[] data)
        {
            int size = Math.Min(Math.Min(contactCode.Length, address.Length), hex.Length);
            data = new bool[size];

            StringBuilder sb = new StringBuilder();
            sb.Append($"<01#RCP{size}");
            for (int i = 0; i < size; i++)
                sb.Append($"{contactCode[i]}{address[i]:D3}{hex[i]:X}");

            string command = sb.ToString();
            command = GetBCCMessage(command);

            byte[] sendBytes = Encoding.ASCII.GetBytes(command);
            if (GetReply(sendBytes, out byte[] recvBytes))
            {
                string reply = Encoding.ASCII.GetString(recvBytes);
                int length = Math.Min(reply.Length - 9, size);
                for (int i = 0; i < length; i++)
                {
                    int index = 6 + i;
                    int value = reply[index] - '0';
                    data[i] = value > 0;
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// 접점 에리어 리드(워드 단위 블록)
        /// </summary>
        /// <param name="contactCode"></param>
        /// <param name="address"></param>
        /// <param name="size"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool GetDIOData(string contactCode, int address, int size, out ushort[] data)
        {
            data = new ushort[size];
            string command = $"<01#RCC{contactCode}{address:D4}{address + size - 1:D4}";
            command = GetBCCMessage(command);

            byte[] sendBytes = Encoding.ASCII.GetBytes(command);
            if (GetReply(sendBytes, out byte[] recvBytes))
            {
                string reply = Encoding.ASCII.GetString(recvBytes);
                int length = Math.Min((reply.Length - 9) / 4, size);
                for (int i = 0; i < length; i++)
                {
                    int index = 6 + i * 4;
                    string sValue = $"{reply.Substring(index + 2, 2)}{reply.Substring(index, 2)}";
                    ushort value = Convert.ToUInt16(sValue, 16);
                    data[i] = value;
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// 접점 에리어 라이트(단점)
        /// </summary>
        /// <param name="contactCode"></param>
        /// <param name="address"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool SetDIOData(string contactCode, int address, int hex, bool data)
        {
            int value = data ? 1 : 0;
            string command = $"<01#WCS{contactCode}{address:D3}{hex:X}{value}";
            command = GetBCCMessage(command);

            byte[] sendBytes = Encoding.ASCII.GetBytes(command);
            return GetReply(sendBytes, out byte[] recvBytes);
        }

        /// <summary>
        /// 접점 에리어 라이트(복수점)
        /// </summary>
        /// <param name="contactCode"></param>
        /// <param name="address"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool SetDIOData(string[] contactCode, int[] address, int[] hex, bool[] data)
        {
            int size = Math.Min(Math.Min(Math.Min(contactCode.Length, address.Length), hex.Length), data.Length);

            StringBuilder sb = new StringBuilder();
            sb.Append($"<01#WCP{size}");
            for (int i = 0; i < size; i++)
            {
                int value = data[i] ? 1 : 0;
                sb.Append($"{contactCode[i]}{address[i]:D3}{hex[i]:X}{value}");
            }

            string command = sb.ToString();
            command = GetBCCMessage(command);

            byte[] sendBytes = Encoding.ASCII.GetBytes(command);
            return GetReply(sendBytes, out byte[] recvBytes);
        }

        /// <summary>
        /// 접점 에리어 라이트(워드 단위 블록)
        /// </summary>
        /// <param name="contactCode"></param>
        /// <param name="address"></param>
        /// <param name="size"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool SetDIOData(string contactCode, int address, int size, ushort[] data)
        {
            size = Math.Min(size, data.Length);

            StringBuilder sb = new StringBuilder();
            sb.Append($"<01#WCC{contactCode}{address:D4}{address + size - 1:D4}");
            for (int i = 0; i < size; i++)
            {
                byte[] bitData = BitConverter.GetBytes(data[i]);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(bitData);
                sb.Append($"{bitData[0]:X2}{bitData[1]:X2}");
            }

            string command = sb.ToString();
            command = GetBCCMessage(command);

            byte[] sendBytes = Encoding.ASCII.GetBytes(command);
            return GetReply(sendBytes, out byte[] recvBytes);
        }

        /// <summary>
        /// 데이터 에리어 리드
        /// </summary>
        /// <param name="address"></param>
        /// <param name="size"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool GetDTData(int address, int size, out ushort[] data)
        {
            data = new ushort[size];

            string command = $"<01#RDD{address:D5}{address + size - 1:D5}";
            command = GetBCCMessage(command);

            byte[] sendBytes = Encoding.ASCII.GetBytes(command);
            if (GetReply(sendBytes, out byte[] recvBytes))
            {
                string reply = Encoding.ASCII.GetString(recvBytes);
                int length = Math.Min((reply.Length - 9) / 4, size);
                for (int i = 0; i < length; i++)
                {
                    int index = 6 + i * 4;
                    string sValue = $"{reply.Substring(index + 2, 2)}{reply.Substring(index, 2)}";
                    ushort value = Convert.ToUInt16(sValue, 16);
                    data[i] = value;
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// 데이터 에리어 라이트
        /// </summary>
        /// <param name="address"></param>
        /// <param name="size"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool SetDTData(int address, int size, ushort[] data)
        {
            size = Math.Min(size, data.Length);

            StringBuilder sb = new StringBuilder();
            sb.Append($"<01#WDD{address:D5}{address + size - 1:D5}");
            for (int i = 0; i < size; i++)
            {
                byte[] bitData = BitConverter.GetBytes(data[i]);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(bitData);
                sb.Append($"{bitData[0]:X2}{bitData[1]:X2}");
            }

            string command = sb.ToString();
            command = GetBCCMessage(command);

            byte[] sendBytes = Encoding.ASCII.GetBytes(command);
            return GetReply(sendBytes, out byte[] recvBytes);
        }

        private bool GetReply(byte[] sendBytes, out byte[] recvBytes)
        {
            recvBytes = m_comm.SendReceiveMessage(sendBytes);
            if (CheckBCCError(recvBytes) || CheckReplyError(recvBytes))
            {
                m_logWriter.Log($"SendRecvError.\r\n{Encoding.ASCII.GetString(sendBytes)}\r\n{Encoding.ASCII.GetString(recvBytes)}");
                return false;
            }
            else
                return true;
        }

        private string GetBCCMessage(string message)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(message);
            byte bcc = 0;
            foreach (byte b in bytes)
                bcc ^= b;

            return $"{message}{bcc:X2}{(char)0x0D}";
        }

        private bool CheckBCCError(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 3)
                return true;

            string reply = Encoding.ASCII.GetString(bytes);
            byte bcc = 0;
            for (int i = 0; i < bytes.Length - 3; i++)
                bcc ^= bytes[i];

            if (!string.Equals(reply.Substring(reply.Length - 3, 2), $"{bcc:X2}"))
                return true;
            else
                return false;
        }

        private bool CheckReplyError(byte[] recvBytes)
        {
            if (recvBytes == null || recvBytes.Length < 9 || recvBytes[3] == (byte)'!' || recvBytes[3] != (byte)'$')
                return true;
            else
                return false;
        }
    }
}