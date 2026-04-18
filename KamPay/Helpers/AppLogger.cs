using System;

namespace KamPay.Helpers
{
    public static class AppLogger
    {
        public static void DebugLog(string message)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(message);
#endif
        }

        public static void DebugLog(string message, Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"{message} | Exception: {ex.Message}");
#endif
        }
    }
}
