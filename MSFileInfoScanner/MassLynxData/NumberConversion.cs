using System;

namespace MSFileInfoScanner.MassLynxData
{
    internal static class NumberConversion
    {
        private const long OFFSET_4 = 4294967296L;
        private const long MAX_INT_4 = 2147483647;
        private const int OFFSET_2 = 65536;

        private const short MAX_INT_2 = 32767;
        public static int UnsignedToInt32(long value)
        {
            if (value <= MAX_INT_4)
            {
                return (int)value;
            }

            return (int)(value - OFFSET_4);
        }

        public static long Int32ToUnsigned(int value)
        {
            if (value < 0)
            {
                return value + OFFSET_4;
            }

            return value;
        }

        public static short UnsignedToInt16(int value)
        {
            if (value is < 0 or >= OFFSET_2)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (value <= MAX_INT_2)
            {
                return (short)value;
            }

            return (short)(value - OFFSET_2);
        }

        public static int Int16ToUnsigned(short value)
        {
            if (value < 0)
            {
                return value + OFFSET_2;
            }

            return value;
        }

        public static bool ValueToBool(int value)
        {
            return value != 0;
        }
    }
}
