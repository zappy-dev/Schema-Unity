using System.Linq;

namespace Schema.Core.Data
{
    public partial class DataScheme
    {
        /// <summary>
        /// Builds the copy of this scheme that publishing writes out: only the columns marked <see cref="AttributeDefinition.ShouldPublish"/>, and every row.
        /// </summary>
        public SchemaResult<DataScheme> CreatePublishedCopy(SchemaContext context)
        {
            var res = SchemaResult<DataScheme>.New(context);
            var publishScheme = new DataScheme(SchemeName);

            var publishedAttributes = GetAttributes(a => a.ShouldPublish).ToList();
            foreach (var publishedAttr in publishedAttributes)
            {
                publishScheme.AddAttribute(context, publishedAttr.Clone() as AttributeDefinition);
            }

            foreach (var entry in entries)
            {
                var publishedEntry = new DataEntry();
                foreach (var publishedAttr in publishedAttributes)
                {
                    publishedEntry.SetData(context, publishedAttr.AttributeName, entry.GetDataDirect(publishedAttr));
                }

                publishScheme.AddEntry(context, publishedEntry);
            }

            return res.Pass(publishScheme);
        }
    }
}
