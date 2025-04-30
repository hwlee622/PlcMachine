using System;

namespace PlcMachine
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

        public override void GetContactArea(string address, out bool value, out DateTime updatedTime)
        {
            value = false;
            updatedTime = DateTime.MinValue;
        }

        public override void SetContactArea(string address, bool value, bool waitUpdate = false)
        {
        }

        public override void GetDataArea(int address, int length, out string value, out DateTime updatedTime)
        {
            value = string.Empty;
            updatedTime = DateTime.MinValue;
        }

        public override void GetDataArea(int address, out short value, out DateTime updatedTime)
        {
            value = 0;
            updatedTime = DateTime.MinValue;
        }

        public override void GetDataArea(int address, out int value, out DateTime updatedTime)
        {
            value = 0;
            updatedTime = DateTime.MinValue;
        }

        public override void SetDataArea(int address, int length, string value, bool waitUpdate = false)
        {
        }

        public override void SetDataArea(int address, short value, bool waitUpdate = false)
        {
        }

        public override void SetDataArea(int address, int value, bool waitUpdate = false)
        {
        }
    }
}