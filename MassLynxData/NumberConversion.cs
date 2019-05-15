using System;

namespace MSFileInfoScanner.MassLynxData
{

    internal static class NumberConversion
    {

        private const long OFFSET_4 = 4294967296L;
        private const long MAXINT_4 = 2147483647;
        private const Int32 OFFSET_2 = 65536;

        private const Int16 MAXINT_2 = 32767;
        public static Int32 UnsignedToInt32(long value)
        {
            if (value <= MAXINT_4)
            {
                return (Int32)value;
            }
            else
            {
                return (Int32)(value - OFFSET_4);
            }
        }

        public static long Int32ToUnsigned(Int32 value)
        {
            if (value < 0)
            {
                return value + OFFSET_4;
            }

            return value;
        }

        public static Int16 UnsignedToInt16(Int32 value)
        {
            if (value < 0 || value >= OFFSET_2)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (value <= MAXINT_2)
            {
                return (Int16)value;
            }
            else
            {
                return (Int16)(value - OFFSET_2);
            }
        }

        public static Int32 Int16ToUnsigned(Int16 value)
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
