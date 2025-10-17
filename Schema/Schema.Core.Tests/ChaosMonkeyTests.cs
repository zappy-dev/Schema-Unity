using Moq;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Tests.Ext;

namespace Schema.Core.Tests;

[TestFixture]
[Ignore("Not implemented yet")]
public class ChaosMonkeyTests
{
    private static SchemaContext testContext = new SchemaContext
    {
        Driver = nameof(ChaosMonkeyTests),
    };

    private enum ChaosOperation
    {
        ADD = 0,
        EDIT = 1,
        DELETE = 2,
        COUNT
    }

    private Random chaosRandom;


    private Mock<IFileSystem> _mockFileSystem;

    [SetUp]
    public async Task OnTestSetup()
    {
        chaosRandom = new Random(12345);
        
        (_mockFileSystem, _) = await TestFixtureSetup.Initialize(testContext);
    }

    public DataScheme ChaosSchemeFactory()
    {
        var scheme = new DataScheme("ChaosScheme");
        
        // do chaos operations..
        var numOperations = chaosRandom.LogNext(1, 100);
        
        for (int opIdx = 0; opIdx < numOperations; opIdx++)
        {
            var opType = chaosRandom.LogNext(0, 2);
            SchemaResult operationRes;
            switch (opType)
            {
                case 0:
                    operationRes = ChaosSchemeAttributeOperation(scheme);
                    break;
                case 1:
                default:
                    operationRes = ChaosSchemeEntryOperation(scheme);
                    break;
            }
            operationRes.AssertPassed();
        }

        return scheme;
    }

    private SchemaResult ChaosSchemeEntryOperation(DataScheme scheme)
    {
        var availableOperations = (scheme.EntryCount > 0) ? new []
        {
            ChaosOperation.ADD,
            ChaosOperation.EDIT,
            ChaosOperation.DELETE
        } : new[]
        {
            ChaosOperation.ADD
        };

        var operation = availableOperations.Random(chaosRandom);
        switch (operation)
        {
            case  ChaosOperation.ADD:
                return scheme.AddEntry(testContext, ChaosEntryFactory());
            case ChaosOperation.DELETE:
                var randomEntryIdx = chaosRandom.LogNext(0, scheme.EntryCount);
                var entry = scheme.GetEntry(randomEntryIdx);
                return scheme.DeleteEntry(testContext, entry);
            case ChaosOperation.EDIT:
            default:
                return SchemaResult.Pass();
        }
    }

    private DataEntry ChaosEntryFactory()
    {
        var entry = new DataEntry();

        return entry;
    }

    private SchemaResult ChaosSchemeAttributeOperation(DataScheme scheme)
    {
        var availableOperations = (scheme.AttributeCount > 0) ? new []
        {
            ChaosOperation.ADD,
            ChaosOperation.EDIT,
            ChaosOperation.DELETE
        } : new[]
        {
            ChaosOperation.ADD
        };
        
        var operation = availableOperations.Random(chaosRandom);
        switch (operation)
        {
            case ChaosOperation.ADD:
                return scheme.AddAttribute(testContext, AttributeChaosFactory(scheme));
            case ChaosOperation.DELETE:
                var randomAttributeIdx = chaosRandom.LogNext(0, scheme.AttributeCount);
                if (!scheme.GetAttribute(randomAttributeIdx).Try(out var attribute, out var error))
                {
                    return error.Cast();
                }
                return scheme.DeleteAttribute(testContext, attribute);
            case ChaosOperation.EDIT:
            default:
                // TODO:
                return SchemaResult.Pass();
                break;
        }
    }

    
    public AttributeDefinition AttributeChaosFactory(DataScheme scheme)
    {
        // creating.. unique names?
        var attributeName = Guid.NewGuid().ToString().Substring(0, 8);
        var dataType = DataType.BuiltInTypes.ToArray().Random(chaosRandom);
        var attribute = new AttributeDefinition(scheme, attributeName, dataType);
        
        return attribute;
    }
    
    [Test]
    public void StartChaos()
    {
        var scheme = ChaosSchemeFactory();
        
        TestContext.Out.WriteLine(scheme.ToString(true));

        var loadRes = Schema.LoadDataScheme(testContext, scheme, true, true);
        loadRes.AssertPassed();
    }
    
    
}