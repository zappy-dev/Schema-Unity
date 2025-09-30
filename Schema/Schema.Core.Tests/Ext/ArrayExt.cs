namespace Schema.Core.Tests.Ext;

public static class ArrayExt
{
    public static T Random<T>(this T[] array, Random random)
    {
        var res = array[random.LogNext(0, array.Length - 1)];
        return res;
    }
}