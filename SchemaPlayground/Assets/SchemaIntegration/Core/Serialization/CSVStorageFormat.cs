using System.IO;
using System.Linq;
using System.Text;

namespace Schema.Core.Serialization
{
    public class CSVStorageFormat : IStorageFormat<DataScheme>
    {
        public string Extension => "csv";
        public DataScheme Load(string filePath)
        {
            var schemaName = Path.GetFileNameWithoutExtension(filePath);
            var importedSchema = new DataScheme(schemaName);
            var rows = File.ReadAllLines(filePath);
            
            var header = rows[0].Split(',');
            
            importedSchema.Attributes.AddRange(header.Select(h => new AttributeDefinition
            {
                AttributeName = h,
                DataType = DataType.String, // TODO: determine datatype, maybe scan entries or hint in header name, e.g header (type)
                DefaultValue = string.Empty,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth,
            }));

            for (var rowIdx = 1; rowIdx < rows.Length; rowIdx++)
            {
                var row = rows[rowIdx];
                var entries = row.Split(',');

                var entry = new DataEntry();
                for (var colIdx = 0; colIdx < header.Length; colIdx++)
                {
                    var attributeName = header[colIdx];
                    
                    entry.SetData(attributeName, entries[colIdx]);
                }
                
                importedSchema.Entries.Add(entry);
            }

            return importedSchema;
        }

        public void Save(string filePath, DataScheme schema)
        {
            StringBuilder csvContent = new StringBuilder();

            // Add headers
            int attributeCount = schema.Attributes.Count;
            for (int i = 0; i < attributeCount; i++)
            {
                var attribute = schema.Attributes[i];
                
                csvContent.Append(attribute.AttributeName);

                // fence posting
                if (i != attributeCount - 1)
                {
                    csvContent.Append(",");
                }
            }
            csvContent.AppendLine();

            // Add data rows
            foreach (var entry in schema.Entries)
            {
                for (int i = 0; i < attributeCount; i++)
                {
                    csvContent.Append(entry.GetData(schema.Attributes[i].AttributeName));
                    
                    // fence posting
                    if (i != attributeCount - 1)
                    {
                        csvContent.Append(",");
                    }
                }
                csvContent.AppendLine();
            }

            // Write to file
            File.WriteAllText(filePath, csvContent.ToString());
        }
    }
}