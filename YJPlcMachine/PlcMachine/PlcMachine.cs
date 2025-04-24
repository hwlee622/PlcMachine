using System;
using System.Collections.Generic;
using System.Threading;

namespace YJPlcMachine
{
    /// <summary>
    /// 설정된 PLC 영역 값을 주기적으로 갱신하는 클래스.
    /// 내부 스레드에서 통신을 처리하고 있으므로 사용자는 데이터를 Get, Set 하기만 하면 된다.
    /// PLC 영역 정보를 단순하게 2byte 데이터인 ushort 값으로 저장한다.
    /// 각 영역에 대한 정보를 short로 사용할지, int로 사용할지, ASCII로 사용할지 등은 데이터를 Get하는 함수에서 적절히 파싱하여 반환한다.
    /// </summary>
    public abstract class PlcMachine
    {
        #region PlcData

        /// <summary>
        /// PLC 데이터를 저장하고 관리하는 클래스.
        /// 읽기 작업 쓰기 작업 별로 권한 부여하여 멀티 스레드 환경에서 안전하게 데이터를 관리할 수 있다.
        /// </summary>
        protected class PlcData
        {
            internal PlcData(int length)
            {
                m_data = new ushort[length];
            }

            private ushort[] m_data;
            private ReaderWriterLockSlim m_lock = new ReaderWriterLockSlim();

            /// <summary>
            /// 저장된 데이터를 불러오는 함수. 모든 데이터는 복사본으로 제공된다.
            /// PlcMachine 외부에서 직접 사용해서는 안된다.
            /// </summary>
            /// <param name="index">불러올 시작 주소</param>
            /// <param name="length">불러올 길이</param>
            /// <returns>복사된 데이터</returns>
            internal ushort[] GetData(int index, int length)
            {
                m_lock.EnterReadLock();
                try
                {
                    length = Math.Min(length, m_data.Length - index);
                    var result = new ushort[length];
                    Array.Copy(m_data, index, result, 0, length);
                    return result;
                }
                finally
                {
                    m_lock.ExitReadLock();
                }
            }

            /// <summary>
            /// 데이터를 새로 갱신하는 함수.
            /// PlcMachine 외부에서 사용할 일은 없고, 사용해서도 안된다.
            /// PlcMachine 내부에서 데이터 갱신용으로 사용.
            /// </summary>
            /// <param name="index">저장할 시작 주소</param>
            /// <param name="value">저장할 데이터</param>
            internal void SetData(int index, ushort[] value)
            {
                m_lock.EnterWriteLock();
                try
                {
                    if (value != null)
                    {
                        int length = Math.Min(value.Length, m_data.Length - index);
                        Array.Copy(value, 0, m_data, index, length);
                    }
                }
                finally
                {
                    m_lock.ExitWriteLock();
                }
            }

            /// <summary>
            /// 데이터를 클리어하는 함수.
            /// PlcMachine이 종료될 경우 호출한다.
            /// PlcMachine 내부에서 사용하는 함수. 외부에서 사용해서는 안된다.
            /// </summary>
            internal void ClearData()
            {
                m_lock.EnterWriteLock();
                try
                {
                    int length = m_data.Length;
                    m_data = new ushort[length];
                }
                finally
                {
                    m_lock.ExitWriteLock();
                }
            }
        }

        #endregion PlcData

        #region ScanAddressData

        /// <summary>
        /// PlcMachine이 Scan해야 할 영역 주소를 관리하는 클래스. 모든 PLC 영역을 전부 스캔할 수는 없다.
        /// PlcMachine의 Get, Set 함수가 실행 되었을 때 해당하는 영역이 등록 되었는지 확인 후 등록 되지 않았다면 등록한다.
        /// </summary>
        protected class ScanAddressData
        {
            internal const int SCANSIZE = 250;
            private readonly Dictionary<string, Dictionary<int, DateTime>> m_scanCodeAddressDict = new Dictionary<string, Dictionary<int, DateTime>>();
            private ReaderWriterLockSlim m_lock = new ReaderWriterLockSlim();

            /// <summary>
            /// 영역 범위가 Scan 영역으로 등록되었는지 확인 후 등록하는 함수.
            /// </summary>
            /// <param name="code">영역 코드</param>
            /// <param name="address">시작 주소</param>
            /// <param name="length">영역 길이</param>
            /// <returns>요소가 추가되었다면 true, 이미 요소가 있다면 false</returns>
            internal bool SetScanAddress(string code, int address, int length)
            {
                bool result = false;
                int firstAddressBlock = address / SCANSIZE;
                int finalAddressBlock = (address + length - 1) / SCANSIZE;

                for (int i = firstAddressBlock; i <= finalAddressBlock; i++)
                {
                    int addressBlock = SCANSIZE * i;
                    if (!IsRegisteredAddress(code, addressBlock))
                    {
                        RegisterAddress(code, addressBlock);
                        result = true;
                    }
                }

                return result;
            }

            private bool IsRegisteredAddress(string code, int address)
            {
                m_lock.EnterWriteLock();
                try
                {
                    if (m_scanCodeAddressDict.TryGetValue(code, out var addressDict) && addressDict.ContainsKey(address))
                    {
                        addressDict[address] = DateTime.Now;
                        return true;
                    }
                    return false;
                }
                finally
                {
                    m_lock.ExitWriteLock();
                }
            }

            private void RegisterAddress(string code, int address)
            {
                m_lock.EnterWriteLock();
                try
                {
                    if (!m_scanCodeAddressDict.TryGetValue(code, out _))
                        m_scanCodeAddressDict.Add(code, new Dictionary<int, DateTime>());
                    if (!m_scanCodeAddressDict[code].TryGetValue(address, out _))
                        m_scanCodeAddressDict[code].Add(address, DateTime.Now);
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
                    if (m_scanCodeAddressDict.TryGetValue(code, out var addressDict))
                        addressList.AddRange(addressDict.Keys);
                }
                finally
                {
                    m_lock.ExitReadLock();
                }
                return addressList;
            }
        }

        #endregion ScanAddressData

        public const int MaxDataAreaAddress = 50000;
        public const int MaxContactAddress = 1000;

        public Action OnDataUpdated;

        protected readonly Dictionary<string, PlcData> m_plcAreaDict = new Dictionary<string, PlcData>();
        protected readonly ScanAddressData m_scanAddressData = new ScanAddressData();

        public abstract void CreateDevice();

        public abstract void CloseDevice();

        /// <param name="address">접점 주소</param>
        /// <param name="value">접점 정보</param>
        public abstract void GetContactArea(string address, out bool value);

        /// <param name="address">접점 주소</param>
        /// <param name="value">접점 값</param>
        /// <param name="waitUpdate">적용 값이 정상적으로 적용될 때 까지 대기할지 결정하는 옵션. Set이후 즉각적인 Get 등이 필요할 경우 활성화</param>
        public abstract void SetContactArea(string address, bool value, bool waitUpdate = false);

        /// <param name="address">영역 주소</param>
        /// <param name="length">영역 길이</param>
        /// <param name="value">ASCII 영역 정보</param>
        public abstract void GetDataArea(int address, int length, out string value);

        /// <param name="address">영역 주소</param>
        /// <param name="value">short 영역 정보</param>
        public abstract void GetDataArea(int address, out short value);

        /// <param name="address">영역 주소</param>
        /// <param name="value">int 영역 정보</param>
        public abstract void GetDataArea(int address, out int value);

        /// <param name="address">영역 주소</param>
        /// <param name="length">영역 길이</param>
        /// <param name="value">ASCII 영역 값</param>
        /// <param name="waitUpdate">적용 값이 정상적으로 적용될 때 까지 대기할지 결정하는 옵션. Set이후 즉각적인 Get 등이 필요할 경우 활성화</param>
        public abstract void SetDataArea(int address, int length, string value, bool waitUpdate = false);

        /// <param name="address">영역 주소</param>
        /// <param name="value">short 영역 값</param>
        /// <param name="waitUpdate">적용 값이 정상적으로 적용될 때 까지 대기할지 결정하는 옵션. Set이후 즉각적인 Get 등이 필요할 경우 활성화</param>
        public abstract void SetDataArea(int address, short value, bool waitUpdate = false);

        /// <param name="address">영역 주소</param>
        /// <param name="value">int 영역 값</param>
        /// <param name="waitUpdate">적용 값이 정상적으로 적용될 때 까지 대기할지 결정하는 옵션. Set이후 즉각적인 Get 등이 필요할 경우 활성화</param>
        public abstract void SetDataArea(int address, int value, bool waitUpdate = false);

        /// <summary>
        /// 영역 정보가 최신화 될 때까지 대기하는 함수.
        /// OnDataUpdated 한번 확인하는 것은 함수 발생 시점에 따라 신뢰성이 없을 수 있으니 두번 확인한다.
        /// </summary>
        protected void WaitScanFinish()
        {
            int scanCount = 0;
            ManualResetEvent scanFinishedEvent = new ManualResetEvent(false);
            Action handler = () =>
            {
                scanCount++;
                if (scanCount >= 2)
                    scanFinishedEvent.Set();
            };

            try
            {
                OnDataUpdated += handler;
                scanFinishedEvent.WaitOne(5000);
            }
            finally
            {
                OnDataUpdated -= handler;
            }
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
    }
}