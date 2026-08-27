namespace Common
{
    public enum Ports
    {
        LeapMotion = 30050,
        MyGaze = 30051,
        TobiiEyeX = 30052,
    }

    public static class LeapMotionEvents
    {
        public const string IS_CONNECTED = "isConnected";
        public const string IS_HAND_VISIBLE = "isHandVisible";
        public const string IS_HAND_CLOSE = "isHandClose";
    }
}
