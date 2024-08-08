namespace java_cup
{
    using System;

    public class Support
    {
        private static DateTime epoch = new DateTime(0x7b2, 1, 1, 0, 0, 0, 0);

        public static long currentTimeMillis()
        {
            TimeSpan span = (TimeSpan) (DateTime.Now - epoch);
            return (long) span.TotalMilliseconds;
        }
    }
}

