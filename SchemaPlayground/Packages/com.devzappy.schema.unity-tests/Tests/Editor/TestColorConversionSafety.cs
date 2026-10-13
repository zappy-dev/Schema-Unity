using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Schema.Core;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Serialization;
using Schema.Runtime.Type;
using UnityEngine;

namespace Schema.Unity.Editor.Tests
{
    /// <summary>
    /// Color columns: what a blank cell turns into, and what survives a save. Acceptance criteria AC2 and AC4 of the conversion and
    /// load safety fixes (docs/upstream-fixes.md).
    /// </summary>
    [TestFixture]
    public class TestColorConversionSafety
    {
        private static readonly SchemaContext Context = new SchemaContext
        {
            Driver = nameof(TestColorConversionSafety)
        };

        /// <summary>A table "Items" with an identifier column and a Text column "Tint" holding the given cells, one row each.</summary>
        private static DataScheme ItemsWithTextTints(params string[] tints)
        {
            var scheme = new DataScheme("Items");
            scheme.AddAttribute(Context, "ID", DataType.Text, isIdentifier: true);
            scheme.AddAttribute(Context, "Tint", DataType.Text);
            for (var i = 0; i < tints.Length; i++)
            {
                var entry = new DataEntry();
                entry.SetData(Context, "ID", "Item_" + (i + 1));
                entry.SetData(Context, "Tint", tints[i]);
                scheme.AddEntry(Context, entry);
            }

            scheme.SetDirty(Context, false);
            return scheme;
        }

        private static List<object> Tints(DataScheme scheme) => scheme.AllEntries.Select(e => e.GetDataDirect("Tint")).ToList();

        // ---- AC2: a blank cell is a Color, or the conversion says why not

        [Test]
        public void AC2_BlankCellsConvertedToColor_BecomeValidColors_AndTheSchemeValidates()
        {
            var items = ItemsWithTextTints("#FF0000", "", "   ", null);

            var result = items.ConvertAttributeType(Context, "Tint", new ColorDataType());

            Assert.That(result.Passed, Is.True, result.Message);
            var tints = Tints(items);
            Assert.That(tints, Is.All.InstanceOf<Color>(), "a blank cell must not be left as a string: " + string.Join(", ", tints));
            Assert.That(tints[0], Is.EqualTo(Color.red));
            Assert.That(tints.Skip(1), Is.All.EqualTo(Color.black), "a blank cell is the type's default colour, black");
            Assert.That(Schema.Core.Schema.IsValidScheme(Context, items).Passed, Is.True, "so the scheme still validates and can be published");
        }

        [Test]
        public void AC2_ABlankCellBecomesTheDefaultColorOfTheType()
        {
            var items = ItemsWithTextTints("#FF0000", "");

            items.ConvertAttributeType(Context, "Tint", new ColorDataType("#FFFFFF")).AssertPassedForTest();

            Assert.That(Tints(items)[1], Is.EqualTo(Color.white));
        }

        [Test]
        public void AC2_ABlankColorCell_DoesNotMakeThePublishedCopyDropTheRow()
        {
            var items = ItemsWithTextTints("#FF0000", "", "#00FF00");
            items.ConvertAttributeType(Context, "Tint", new ColorDataType()).AssertPassedForTest();

            var copy = items.CreatePublishedCopy(Context);

            Assert.That(copy.Passed, Is.True, copy.Message);
            Assert.That(copy.Result.EntryCount, Is.EqualTo(3), "every row is published");
        }

        [Test, TestCase(null), TestCase(""), TestCase("   ")]
        public void AC2_ConvertValueOfABlankValue_IsAColor_NotAString(string blank)
        {
            var conversion = new ColorDataType().ConvertValue(Context, blank);

            Assert.That(conversion.Passed, Is.True);
            Assert.That(conversion.Result, Is.InstanceOf<Color>(), "a string here fails the type's own IsValidValue");
            Assert.That(new ColorDataType().IsValidValue(Context, conversion.Result).Passed, Is.True);
        }

        // ---- findings 7 and 8 (see docs/upstream-fixes.md)

        [Test]
        [TestCase("red", 1f, 0f, 0f)]
        [TestCase("  cyan  ", 0f, 1f, 1f)]
        [TestCase("#00F", 0f, 0f, 1f)]
        [TestCase("00ff00", 0f, 1f, 0f)]
        public void B7_ANamedColorOrAShortHexConverts(string text, float r, float g, float b)
        {
            var items = ItemsWithTextTints(text);

            var result = items.ConvertAttributeType(Context, "Tint", new ColorDataType());

            Assert.That(result.Passed, Is.True, result.Message);
            Assert.That(Tints(items)[0], Is.EqualTo(new Color(r, g, b, 1f)));
        }

        [Test]
        public void B7_ANameThatIsNotAColor_StillFails()
        {
            var items = ItemsWithTextTints("notacolor");

            Assert.That(items.ConvertAttributeType(Context, "Tint", new ColorDataType()).Failed, Is.True);
        }

        [Test]
        public void B8_ANewRowInAColorColumn_HoldsAColor_AndTheSchemeValidates()
        {
            var items = ItemsWithTextTints("#FF0000");
            items.ConvertAttributeType(Context, "Tint", new ColorDataType()).AssertPassedForTest();

            var added = items.CreateNewEmptyEntry(Context);

            Assert.That(added.Passed, Is.True, added.Message);
            Assert.That(added.Result.GetDataDirect("Tint"), Is.InstanceOf<Color>(), "the column default is hex text, a new row must not hold it as is");
            Assert.That(Schema.Core.Schema.IsValidScheme(Context, items).Passed, Is.True);
        }

        [Test]
        public void B8_AColorColumnAddedToATableWithRows_FillsThemWithColors()
        {
            var items = ItemsWithTextTints("a", "b");

            items.AddAttribute(Context, "Glow", new ColorDataType("#00FF00"));

            Assert.That(items.AllEntries.Select(e => e.GetDataDirect("Glow")), Is.All.EqualTo(Color.green));
        }

        // ---- AC4: alpha survives save and reload

        private static string Save(DataScheme scheme) =>
            new JsonSchemeStorageFormat(FileSystemFactory.DefaultFileSystem).Serialize(Context, scheme).Result;

        private static DataScheme Reload(string json)
        {
            var scheme = new JsonSchemeStorageFormat(FileSystemFactory.DefaultFileSystem).Deserialize(Context, json).Result;
            var load = Schema.Core.Schema.LoadDataScheme(Context, scheme, overwriteExisting: true, registerManifestEntry: false);
            Assert.That(load.Passed, Is.True, load.Message);
            return scheme;
        }

        private static DataScheme ItemsWithColors(params Color32[] colors)
        {
            var scheme = ItemsWithTextTints(colors.Select(_ => "").ToArray());
            scheme.ConvertAttributeType(Context, "Tint", new ColorDataType()).AssertPassedForTest();
            var row = 0;
            foreach (var entry in scheme.AllEntries) entry.SetData(Context, "Tint", (Color)colors[row++]);
            return scheme;
        }

        private static List<string> SavedTints(string json) =>
            ((JArray)JObject.Parse(json)["Entries"]).Select(e => (string)e["Tint"]).ToList();

        [Test]
        public void AC4_AColorWithAlpha_SurvivesSaveAndReload()
        {
            var original = new Color32(255, 0, 0, 128);
            var items = ItemsWithColors(original);

            var json = Save(items);

            Assert.That(SavedTints(json), Is.EqualTo(new[] { "#FF000080" }), "the alpha is written");
            var reloaded = Reload(json);
            Assert.That((Color32)(Color)Tints(reloaded)[0], Is.EqualTo(original), "and read back unchanged");
            Assert.That(SavedTints(Save(reloaded)), Is.EqualTo(new[] { "#FF000080" }), "so a second save writes the same text");
        }

        [Test]
        public void AC4_AFullyTransparentColor_SurvivesSaveAndReload()
        {
            var original = new Color32(255, 255, 255, 0);
            var items = ItemsWithColors(original);

            var json = Save(items);

            Assert.That(SavedTints(json), Is.EqualTo(new[] { "#FFFFFF00" }));
            Assert.That((Color32)(Color)Tints(Reload(json))[0], Is.EqualTo(original));
        }

        [Test]
        public void AC4_AnOpaqueColor_IsStillSavedAsSixDigits()
        {
            var items = ItemsWithColors(new Color32(255, 154, 154, 255), new Color32(0, 0, 0, 255));

            var json = Save(items);

            Assert.That(SavedTints(json), Is.EqualTo(new[] { "#FF9A9A", "#000000" }), "files that hold opaque colours do not change");
        }
    }

    internal static class SchemaResultTestExt
    {
        public static void AssertPassedForTest(this SchemaResult result)
        {
            Assert.That(result.Passed, Is.True, result.Message);
        }
    }
}
