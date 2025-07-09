namespace PlcUtil.PlcMachine
{
    public class PlcMachineNone : PlcMachine
    {
        public PlcMachineNone()
        {
            IsConnected = true;
        }

        public override void CreateDevice()
        {
        }

        public override void CloseDevice()
        {
        }

        protected override bool ScanBitData()
        {
            return true;
        }

        protected override bool ScanWordData()
        {
            return true;
        }

        public override bool GetBitData(string address)
        {
            return false;
        }

        public override void SetBitData(string address, bool value)
        {
        }

        public override string GetWordDataASCII(string address, int length)
        {
            return string.Empty;
        }

        public override short GetWordDataShort(string address)
        {
            return 0;
        }

        public override int GetWordDataInt(string address)
        {
            return 0;
        }

        public override void SetWordDataASCII(string address, int length, string value)
        {
        }

        public override void SetWordDataShort(string address, short value)
        {
        }

        public override void SetWordDataInt(string address, int value)
        {
        }
    }
}