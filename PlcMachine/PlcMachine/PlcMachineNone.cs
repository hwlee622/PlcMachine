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

        public override void GetBitData(string address, out bool value)
        {
            value = false;
        }

        public override void SetBitData(string address, bool value)
        {
        }

        public override void GetWordData(int address, int length, out string value)
        {
            value = string.Empty;
        }

        public override void GetWordData(int address, out short value)
        {
            value = 0;
        }

        public override void GetWordData(int address, out int value)
        {
            value = 0;
        }

        public override void SetWordData(int address, int length, string value)
        {
        }

        public override void SetWordData(int address, short value)
        {
        }

        public override void SetWordData(int address, int value)
        {
        }
    }
}