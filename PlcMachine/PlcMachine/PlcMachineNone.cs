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

        public override string GetWordDataASCII(int address, int length)
        {
            return string.Empty;
        }

        public override short GetWordDataShort(int address)
        {
            return 0;
        }

        public override int GetWordDataInt(int address)
        {
            return 0;
        }

        public override void SetWordDataASCII(int address, int length, string value)
        {
        }

        public override void SetWordDataShort(int address, short value)
        {
        }

        public override void SetWordDataInt(int address, int value)
        {
        }
    }
}