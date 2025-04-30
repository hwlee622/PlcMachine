using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace CommInterface
{
    public class CommInterfaceSerial : Comm
    {
        private string m_port;
        private int m_baudRate;
        private Parity m_parity;
        private int m_dataBit;
        private StopBits m_stopBits;

        private SerialPort m_serial;

        #region Constructors

        public CommInterfaceSerial(string port) : this(port, 9600, Parity.None, 8, StopBits.One)
        {
        }

        public CommInterfaceSerial(string port, int baudRate) : this(port, baudRate, Parity.None, 8, StopBits.One)
        {
        }

        public CommInterfaceSerial(string port, int baudRate, Parity parity) : this(port, baudRate, parity, 8, StopBits.One)
        {
        }

        public CommInterfaceSerial(string port, int baudRate, Parity parity, int dataBit) : this(port, baudRate, parity, dataBit, StopBits.One)
        {
        }

        public CommInterfaceSerial(string port, int baudRate, Parity parity, int dataBit, StopBits stopBits)
        {
            m_port = port;
            m_baudRate = baudRate;
            m_parity = parity;
            m_dataBit = dataBit;
            m_stopBits = stopBits;
        }

        #endregion Constructors

        public override void Start()
        {
            Stop();

            m_serial = new SerialPort(m_port, m_baudRate, m_parity, m_dataBit, m_stopBits);

            base.Start();
        }

        public override void Stop()
        {
            base.Stop();

            if (m_serial != null)
            {
                m_serial.Close();
            }
            m_serial = null;
        }

        public override bool IsConnected()
        {
            bool isConnected = m_serial != null && m_serial.IsOpen;
            return isConnected;
        }

        protected override async Task ConnectTask(CancellationToken token)
        {
            Task cancel = Task.Delay(Timeout.Infinite, token);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(20);
                    bool isConnected = IsConnected();
                    UpdateConnectState(isConnected);
                    if (token.IsCancellationRequested)
                        continue;
                    else if (!isConnected)
                    {
                        m_serial.Open();
                        m_connectExceptionDict.Clear();
                    }
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            }
        }

        protected override async Task WriteAsync(byte[] message)
        {
            await Task.Run(() =>
            {
                OnSendLog?.Invoke(message);
                m_serial.WriteTimeout = WriteTimeout;
                m_serial.Write(message, 0, message.Length);
            });
        }

        protected override async Task ReadAsync()
        {
            await Task.Run(() =>
            {
                var buffer = new byte[m_serial.ReadBufferSize];
                var bufferLength = m_serial.Read(buffer, 0, buffer.Length);
                if (bufferLength > 0)
                {
                    var message = new byte[bufferLength];
                    Array.Copy(buffer, message, bufferLength);
                    OnReceiveLog?.Invoke(message);
                    m_recvQueue.Enqueue(message);
                }
            });
        }
    }
}