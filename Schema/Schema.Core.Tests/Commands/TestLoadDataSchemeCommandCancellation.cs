using Schema.Core.Commands;
using Schema.Core.Data;

namespace Schema.Core.Tests.Commands;

[TestFixture]
public class TestLoadDataSchemeCommandCancellation
{
    internal static SchemaContext Context = new SchemaContext
    {
        Driver = nameof(TestLoadDataSchemeCommandCancellation),
    };
    
    [Test]
    public async Task ExecuteAsync_WithPreCancelledToken_ShouldReturnCancelled()
    {
        var scheme = new DataScheme("CancelScheme");
        var cmd = new LoadDataSchemeCommand(Context, scheme, overwriteExisting: true);

        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await cmd.ExecuteAsync(cts.Token);
        Assert.IsTrue(result.IsCancelled);
    }
} 