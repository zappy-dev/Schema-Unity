namespace Schema.Core.Tests.Ext;

public static class RandomExt
{
    public static int LogNext(this Random random, int inclusive, int exclusive)
    {
        var value = random.Next(inclusive, exclusive);
        return value;
    }
}