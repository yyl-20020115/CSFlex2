namespace java_cup
{
    using System;
    using System.Text;

    public class Integer
    {
        private int value;

        public Integer(int val)
        {
            this.value = val;
        }

        public int intValue() => 
            this.value;

        public static string toHexString(int c)
        {
            StringBuilder builder = new StringBuilder();
            while (c > 0)
            {
                int num = c & 15;
                c = c >> 4;
                if (num >= 10)
                {
                    builder.Insert(0, (char) ((num + 0x61) - 10));
                }
                else
                {
                    builder.Insert(0, (char) (num + 0x30));
                }
            }
            if (builder.Length == 0)
            {
                return "0";
            }
            return builder.ToString();
        }

        public static string toOctalString(int c)
        {
            StringBuilder builder = new StringBuilder();
            while (c > 0)
            {
                int num = c & 7;
                c = c >> 3;
                builder.Insert(0, (char) (num + 0x30));
            }
            if (builder.Length == 0)
            {
                return "0";
            }
            return builder.ToString();
        }
    }
}

