using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CommInterface
{
    public abstract class Comm
    {
        public Action<bool> OnConnectedOrDisconnected;
        public Action<byte[]> OnSendLog;
        public Action<byte[]> OnReceiveLog;
        public Action<Exception> OnError;
        public Action<byte[]> OnReceiveMessage;

        public int WriteTimeout = Timeout.Infinite;
        public int ReadTimeout = Timeout.Infinite;

        #region Properties

        private object m_stxLock = new object();
        private bool m_stxSetted = false;
        private byte[] STX;
        private object m_etxLock = new object();
        private bool m_etxSetted = false;
        private byte[] ETX;
        private AutoResetEvent m_sendrecvEvent = new AutoResetEvent(true);

        protected ConcurrentQueue<byte[]> m_sendQueue = new ConcurrentQueue<byte[]>();
        protected ConcurrentQueue<byte[]> m_recvQueue = new ConcurrentQueue<byte[]>();

        protected Dictionary<Type, bool> m_connectExceptionDict = new Dictionary<Type, bool>();

        protected CancellationTokenSource m_cts = new CancellationTokenSource();

        #endregion Properties

        public virtual void Start()
        {
            m_cts = new CancellationTokenSource();
            Task.Run(() => BufferTask(m_cts.Token));
            Task.Run(() => ConnectTask(m_cts.Token));
            Task.Run(() => SendTask(m_cts.Token));
            Task.Run(() => RecvTask(m_cts.Token));
        }

        public virtual void Stop()
        {
            m_cts.Cancel();
            m_connectExceptionDict.Clear();
        }

        public abstract bool IsConnected();

        public void SetSTX(byte[] stx)
        {
            lock (m_stxLock)
            {
                if (!m_stxSetted)
                {
                    m_stxSetted = true;
                    STX = stx;
                }
            }
        }

        public void SetETX(byte[] etx)
        {
            lock (m_etxLock)
            {
                if (!m_etxSetted)
                {
                    m_etxSetted = true;
                    ETX = etx;
                }
            }
        }

        public void SendMessage(byte[] message)
        {
            if (IsConnected())
                m_sendQueue.Enqueue(message);
        }

        public byte[] SendReceiveMessage(byte[] sendMessage)
        {
            byte[] recvMessage = new byte[0];
            ManualResetEvent recvEvent = new ManualResetEvent(false);

            void Handler(byte[] bytes)
            {
                if (recvEvent.WaitOne(0))
                    return;

                recvMessage = bytes;
                recvEvent.Set();
            }

            m_sendrecvEvent.WaitOne();
            try
            {
                OnReceiveMessage += Handler;
                SendMessage(sendMessage);
                if (!recvEvent.WaitOne(ReadTimeout))
                    throw new TimeoutException($"SendReceive Timeout. ReadTimeout : {ReadTimeout}");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
            finally
            {
                OnReceiveMessage -= Handler;
                m_sendrecvEvent.Set();
            }
            return recvMessage;
        }

        protected abstract Task ConnectTask(CancellationToken token);

        private async Task SendTask(CancellationToken token)
        {
            Task cancel = Task.Delay(Timeout.Infinite, token);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!m_sendQueue.TryDequeue(out var message) || !IsConnected())
                        await Task.Delay(20);
                    else
                    {
                        Task send = WriteAsync(message);
                        await Task.WhenAny(send, cancel);
                        if (token.IsCancellationRequested)
                            continue;
                        await send;
                    }
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            }
        }

        protected abstract Task WriteAsync(byte[] message);

        private async Task RecvTask(CancellationToken token)
        {
            Task cancel = Task.Delay(Timeout.Infinite, token);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!IsConnected())
                        await Task.Delay(20);
                    else
                    {
                        Task read = ReadAsync();
                        await Task.WhenAny(read, cancel);
                        if (token.IsCancellationRequested)
                            continue;
                        await read;
                    }
                }
                catch (Exception ex)
                {
                    ReportError(ex);
                }
            }
        }

        protected abstract Task ReadAsync();

        protected async Task BufferTask(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!m_recvQueue.TryDequeue(out var message))
                        await Task.Delay(20);
                    else
                        EnqueueReceiveMessage(message);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                }
            }
        }

        private List<byte> m_buffer = new List<byte>();

        private void EnqueueReceiveMessage(byte[] buffer)
        {
            m_buffer.AddRange(buffer);
            int stxLength = STX != null ? STX.Length : 0;
            int etxLength = ETX != null ? ETX.Length : 0;

            int stxIndex = FindByteIndex(m_buffer, STX);
            stxIndex = stxIndex >= 0 ? stxIndex : 0;
            int etxIndex = FindByteIndex(m_buffer, ETX);
            etxIndex = ETX != null ? etxIndex : m_buffer.Count - etxLength;

            while (etxIndex != -1)
            {
                int length = etxIndex + etxLength - stxIndex;
                var message = m_buffer.Skip(stxIndex).Take(length).ToArray();
                m_buffer = m_buffer.Skip(etxIndex + etxLength).ToList();

                stxIndex = FindByteIndex(m_buffer, STX);
                stxIndex = stxIndex >= 0 ? stxIndex : 0;
                etxIndex = FindByteIndex(m_buffer, ETX);
                OnReceiveMessage?.Invoke(message);
            }
        }

        private int FindByteIndex(List<byte> buffer, byte[] pattern)
        {
            if (pattern != null)
            {
                for (int i = 0; i < buffer.Count; i++)
                {
                    if (buffer.Skip(i).Take(pattern.Length).SequenceEqual(pattern))
                        return i;
                }
            }
            return -1;
        }

        private bool m_isConnected;

        protected void UpdateConnectState(bool isConnected)
        {
            if (m_isConnected != isConnected)
                OnConnectedOrDisconnected?.Invoke(isConnected);
            m_isConnected = isConnected;
        }

        protected void ReportError(Exception ex)
        {
            var exType = ex.GetType();
            if (!m_connectExceptionDict.ContainsKey(exType))
            {
                OnError?.Invoke(ex);
                m_connectExceptionDict[exType] = true;
            }
        }
    }
}