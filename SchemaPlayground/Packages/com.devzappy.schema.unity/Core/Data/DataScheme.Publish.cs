using System.Linq;

namespace Schema.Core.Data
{
    public partial class DataScheme
    {
        /// <summary>
        /// Builds the copy of this scheme that publishing writes out: only the columns marked <see cref="AttributeDefinition.ShouldPublish"/>, and every row.
        /// </summary>
        /// <remarks>
        /// Fails, naming the scheme, the row, the column and the value, when a row cannot be added to the copy because a value is not valid
        /// for its column. A row is never left out of the copy: publishing a table that is missing rows would lose them without a message.
        /// This scheme is not changed.
        /// </remarks>
        /// <param name="context">Operation context</param>
        /// <returns>The scheme to publish</returns>
        public SchemaResult<DataScheme> CreatePublishedCopy(SchemaContext context)
        {
            var res = SchemaResult<DataScheme>.New(context);
            var publishScheme = new DataScheme(SchemeName);

            var publishedAttributes = GetAttributes(a => a.ShouldPublish).ToList();
            foreach (var publishedAttr in publishedAttributes)
            {
                var addAttributeRes = publishScheme.AddAttribute(context, publishedAttr.Clone() as AttributeDefinition);
                if (addAttributeRes.Failed)
                {
                    return res.Fail($"Cannot publish scheme '{SchemeName}': column '{publishedAttr.AttributeName}' could not be added to the published copy: {addAttributeRes.Message}");
                }
            }

            for (var rowIndex = 0; rowIndex < entries.Count; rowIndex++)
            {
                var entry = entries[rowIndex];
                var publishedEntry = new DataEntry();
                foreach (var publishedAttr in publishedAttributes)
                {
                    var value = entry.GetDataDirect(publishedAttr);
                    var validRes = publishedAttr.IsValidValue(context, value);
                    if (validRes.Failed)
                    {
                        return res.Fail($"Cannot publish scheme '{SchemeName}': {DescribeRow(rowIndex)}, column '{publishedAttr.AttributeName}': " +
                                        $"value '{value}' is not valid: {validRes.Message}");
                    }

                    publishedEntry.SetData(context, publishedAttr.AttributeName, value);
                }

                // The values were validated above, one by one, so that a failure can name the column.
                var addEntryRes = publishScheme.AddEntry(context, publishedEntry, runDataValidation: false);
                if (addEntryRes.Failed)
                {
                    return res.Fail($"Cannot publish scheme '{SchemeName}': {DescribeRow(rowIndex)} could not be added to the published copy: {addEntryRes.Message}");
                }
            }

            return res.Pass(publishScheme);
        }
    }
}
