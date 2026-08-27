namespace Common
{
    public enum Ports
    {
        LeapMotion = 30050,
        MyGaze = 30051,
        TobiiEyeX = 30052,
    }

    public partial class Vector
    {
        public readonly static Vector ZEROS = new() { X = 0, Y = 0, Z = 0 };
        public readonly static Vector ONES = new() { X = 1, Y = 1, Z = 1 };
    }

    namespace LeapMotion
    {
        public static class Events
        {
            public const string IS_CONNECTED = "isConnected";
            public const string IS_HAND_VISIBLE = "isHandVisible";
            public const string IS_HAND_CLOSE = "isHandClose";
        }
    }
}
