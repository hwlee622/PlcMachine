namespace YJPlcMachine
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

        public override void GetContactArea(string address, out bool value)
        {
            value = false;
        }

        public override void SetContactArea(string address, bool value)
        {
        }

        public override void GetDataArea(int address, int length, out string value)
        {
            value = string.Empty;
        }

        public override void GetDataArea(int address, out short value)
        {
            value = 0;
        }

        public override void GetDataArea(int address, out int value)
        {
            value = 0;
        }

        public override void SetDataArea(int address, int length, string value)
        {
        }

        public override void SetDataArea(int address, short value)
        {
        }

        public override void SetDataArea(int address, int value)
        {
        }
    }
}