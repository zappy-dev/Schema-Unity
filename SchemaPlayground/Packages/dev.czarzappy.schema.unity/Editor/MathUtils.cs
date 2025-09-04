namespace Schema.Unity.Editor
{
    internal class MathUtils
    {
        internal static int ClampToInt(long value)
        {
            if (value < (long) int.MinValue)
                return int.MinValue;
            return value > (long) int.MaxValue ? int.MaxValue : (int) value;
        }
    }
}