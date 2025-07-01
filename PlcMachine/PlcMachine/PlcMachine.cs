using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlcUtil.PlcMachine
{
    /// <summary>
    /// 설정된 PLC 영역 값을 주기적으로 갱신하는 클래스.
    /// 내부 스레드에서 통신을 처리하고 있으므로 사용자는 데이터를 Get, Set 하기만 하면 된다.
    /// PLC 영역 정보를 단순하게 2byte 데이터인 ushort 값으로 저장한다.
    /// 각 영역에 대한 정보를 short로 사용할지, int로 사용할지, ASCII로 사용할지 등은 데이터를 Get하는 함수에서 적절히 파싱하여 반환한다.
    /// </summary>
    public abstract class PlcMachine
    {
        #region BitData

        protected class BitData
        {
            /// <summary>
            /// 1bit 데이터를 관리하는 클래스
            /// </summary>
            internal BitData(int length)
            {
                _data = new bool[length];
            }

            private bool[] _data;
            private ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

            internal bool[] GetData(int index, int length)
            {
                _lock.EnterReadLock();
                try
                {
                    var data = new bool[length];
                    if (index >= _data.Length)
                        return data;

                    length = Math.Min(length, _data.Length - index);
                    Array.Copy(_data, index, data, 0, length);
                    return data;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }

            internal void SetData(int index, bool[] data)
            {
                _lock.EnterWriteLock();
                try
                {
                    if (data == null || index >= _data.Length)
                        return;

                    int length = Math.Min(data.Length, _data.Length - index);
                    Array.Copy(data, 0, _data, index, length);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            internal void ClearData()
            {
                _lock.EnterWriteLock();
                try
                {
                    _data = new bool[_data.Length];
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        #endregion BitData

        #region WordData

        /// <summary>
        /// 2byte 데이터를 관리하는 클래스
        /// </summary>
        protected class WordData
        {
            internal WordData(int length)
            {
                _data = new ushort[length];
            }

            private ushort[] _data;
            private ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

            internal ushort[] GetData(int index, int length)
            {
                _lock.EnterReadLock();
                try
                {
                    var data = new ushort[length];
                    if (index >= _data.Length)
                        return data;

                    length = Math.Min(length, _data.Length - index);
                    Array.Copy(_data, index, data, 0, length);
                    return data;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }

            internal void SetData(int index, ushort[] data)
            {
                _lock.EnterWriteLock();
                try
                {
                    if (data == null || index >= _data.Length)
                        return;

                    int length = Math.Min(data.Length, _data.Length - index);
                    Array.Copy(data, 0, _data, index, length);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            internal void ClearData()
            {
                _lock.EnterWriteLock();
                try
                {
                    _data = new ushort[_data.Length];
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        #endregion WordData

        #region ScanAddressData

        /// <summary>
        /// PlcMachine이 Scan해야 할 영역 주소를 관리하는 클래스. 모든 PLC 영역을 전부 스캔할 수는 없다.
        /// PlcMachine의 Get, Set 함수가 실행 되었을 때 해당하는 영역이 등록 되었는지 확인 후 등록 되지 않았다면 등록한다.
        /// </summary>
        protected class ScanAddressData
        {
            private class ScanBlock
            {
                public int Address { get; }
                public DateTime LastAccessTime { get; private set; }

                public ScanBlock(int address)
                {
                    Address = address;
                    LastAccessTime = DateTime.Now;
                }

                public void UpdateAccessTime()
                {
                    LastAccessTime = DateTime.Now;
                }
            }

            private readonly Dictionary<string, Dictionary<int, ScanBlock>> m_scanBlockDict = new Dictionary<string, Dictionary<int, ScanBlock>>();
            private ReaderWriterLockSlim m_lock = new ReaderWriterLockSlim();

            /// <summary>
            /// 영역 범위가 Scan 영역으로 등록되었는지 확인 후 등록하는 함수.
            /// </summary>
            /// <param name="code">영역 코드</param>
            /// <param name="address">시작 주소</param>
            /// <param name="length">영역 길이</param>
            /// <param name="scanSize">스캔할 길이</param>
            /// <returns>요소가 추가되었다면 true, 이미 요소가 있다면 false</returns>
            internal bool SetScanAddress(string code, int address, int length, int scanSize)
            {
                bool result = false;
                int firstAddressBlock = address / scanSize;
                int finalAddressBlock = (address + length - 1) / scanSize;

                for (int i = firstAddressBlock; i <= finalAddressBlock; i++)
                {
                    int addressBlock = scanSize * i;
                    if (!IsRegisteredAddress(code, addressBlock))
                    {
                        RegisterAddress(code, addressBlock);
                        result = true;
                    }
                }

                return result;
            }

            private bool IsRegisteredAddress(string code, int addressBlock)
            {
                m_lock.EnterWriteLock();
                try
                {
                    if (m_scanBlockDict.TryGetValue(code, out var blockDict) && blockDict.TryGetValue(addressBlock, out var scanBlock))
                    {
                        scanBlock.UpdateAccessTime();
                        return true;
                    }
                    return false;
                }
                finally
                {
                    m_lock.ExitWriteLock();
                }
            }

            private void RegisterAddress(string code, int addressBlock)
            {
                m_lock.EnterWriteLock();
                try
                {
                    if (!m_scanBlockDict.TryGetValue(code, out var blockList))
                        m_scanBlockDict.Add(code, new Dictionary<int, ScanBlock>());
                    if (!m_scanBlockDict[code].TryGetValue(addressBlock, out var scanBlock))
                        m_scanBlockDict[code].Add(addressBlock, new ScanBlock(addressBlock));
                }
                finally
                {
                    m_lock.ExitWriteLock();
                }
            }

            /// <summary>
            /// 외부에서 접근하지 않는 오래된 영역 리스트를 삭제하는 함수.
            /// 외부에서 접근하지 않는데 갱신하고 있을 필요가 없다.
            /// </summary>
            /// <param name="expireTimeSpan"></param>
            internal void ExpireOldScanAddress(TimeSpan expireTimeSpan)
            {
                DateTime now = DateTime.Now;

                m_lock.EnterWriteLock();
                try
                {
                    foreach (var blockDict in m_scanBlockDict.Values)
                    {
                        List<int> expireAddressKeyList = new List<int>();
                        foreach (var blockPair in blockDict)
                            if (now - blockPair.Value.LastAccessTime > expireTimeSpan)
                                expireAddressKeyList.Add(blockPair.Key);

                        foreach (var expireAddressKey in expireAddressKeyList)
                            blockDict.Remove(expireAddressKey);
                    }
                }
                finally
                {
                    m_lock.ExitWriteLock();
                }
            }

            /// <summary>
            /// 저장된 영역 리스트를 반환하는 함수.
            /// PlcMachine이 Scan 작업할 때 호출하여 자신이 갱신해야 할 영역 범위를 얻는다.
            /// </summary>
            /// <param name="code"></param>
            /// <returns></returns>
            internal List<int> GetScanAddress(string code)
            {
                List<int> addressList = new List<int>();
                m_lock.EnterReadLock();
                try
                {
                    if (m_scanBlockDict.TryGetValue(code, out var blockDict))
                        foreach (var scanBlock in blockDict.Values)
                            addressList.Add(scanBlock.Address);
                }
                finally
                {
                    m_lock.ExitReadLock();
                }
                return addressList;
            }
        }

        #endregion ScanAddressData

        public bool IsConnected { get; protected set; } = false;
        public Action OnDataUpdated;

        protected readonly ScanAddressData m_scanAddressData = new ScanAddressData();

        protected readonly Dictionary<string, BitData> _bitDataDict = new Dictionary<string, BitData>();
        protected readonly Dictionary<string, WordData> _wordDataDict = new Dictionary<string, WordData>();

        public abstract void CreateDevice();

        public abstract void CloseDevice();

        protected async void ScanDevice(CancellationToken token)
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

                    bool bitScanResult = ScanBitData();
                    bool wordScanResult = ScanWordData();

                    IsConnected = bitScanResult && wordScanResult;
                }
                finally
                {
                    OnDataUpdated?.Invoke();
                }
            }
        }

        protected abstract bool ScanBitData();

        protected abstract bool ScanWordData();

        /// <param name="address">접점 주소</param>
        /// <param name="value">접점 정보</param>
        public abstract void GetBitData(string address, out bool value);

        /// <param name="address">접점 주소</param>
        /// <param name="value">접점 값</param>
        public abstract void SetBitData(string address, bool value);

        /// <param name="address">영역 주소</param>
        /// <param name="length">영역 길이</param>
        /// <param name="value">ASCII 영역 정보</param>
        public abstract void GetWordData(int address, int length, out string value);

        /// <param name="address">영역 주소</param>
        /// <param name="value">short 영역 정보</param>
        public abstract void GetWordData(int address, out short value);

        /// <param name="address">영역 주소</param>
        /// <param name="value">int 영역 정보</param>
        public abstract void GetWordData(int address, out int value);

        /// <param name="address">영역 주소</param>
        /// <param name="length">영역 길이</param>
        /// <param name="value">ASCII 영역 값</param>
        public abstract void SetWordData(int address, int length, string value);

        /// <param name="address">영역 주소</param>
        /// <param name="value">short 영역 값</param>
        public abstract void SetWordData(int address, short value);

        /// <param name="address">영역 주소</param>
        /// <param name="value">int 영역 값</param>
        public abstract void SetWordData(int address, int value);
    }
}