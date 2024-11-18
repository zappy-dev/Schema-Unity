using System;
using System.IO;
using System.Linq;
using System.Text;
using Schema.Core.Data;
using Schema.Core.IO;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Serialization
{
    public class CSVStorageFormat : IStorageFormat<DataScheme>
    {
        public string Extension => "csv";
        public bool TryDeserializeFromFile(string filePath, out DataScheme data)
        {
            throw new NotImplementedException();
        }

        public bool TryDeserialize(string content, out DataScheme data)
        {
            throw new NotImplementedException();
        }

        private readonly IFileSystem fileSystem;

        public CSVStorageFormat(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }
        
        public DataScheme TryDeserializeFromFile(string filePath)
        {
            var schemeName = Path.GetFileNameWithoutExtension(filePath);
            var rows = fileSystem.ReadAllLines(filePath);

            return LoadFromRows(schemeName, rows);
        }

        public DataScheme Deserialize(string content)
        {
            string[] rows = content.Split(new[]
            {
                Environment.NewLine,
            }, StringSplitOptions.None);

            return LoadFromRows(schemeName: "unnamed", rows);
        }

        private DataScheme LoadFromRows(string schemeName, string[] rows)
        {
            var importedScheme = new DataScheme(schemeName);
            var header = rows[0].Split(',');

            bool canLoad = header.Select(h => new AttributeDefinition
            {
                AttributeName = h,
                DataType = DataType.Text, // TODO: determine datatype, maybe scan entries or hint in header name, e.g header (type). Alternatively, use existing scheme's type info
                DefaultValue = string.Empty,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth,
            }).All(a => importedScheme.AddAttribute(a).Passed);

            if (canLoad)
            {
                // TODO: Improve CSV validation and error reporting
                throw new InvalidOperationException("Failed to load data");
            }

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
                
                importedScheme.AddEntry(entry);
            }

            return importedScheme;
        }

        public SchemaResult SerializeToFile(string filePath, DataScheme scheme)
        {
            if (scheme == null)
            {
                return Fail($"Scheme cannot be null", this);
            }
            
            StringBuilder csvContent = new StringBuilder();

            // Add headers
            int attributeCount = scheme.AttributeCount;
            if (attributeCount == 0)
            {
                return Fail($"Scheme cannot be empty", this);
            }
            
            for (int i = 0; i < attributeCount; i++)
            {
                var attribute = scheme.GetAttribute(i);
                
                csvContent.Append(attribute.AttributeName);

                // fence posting
                if (i != attributeCount - 1)
                {
                    csvContent.Append(",");
                }
            }
            csvContent.AppendLine();

            // Add data rows
            foreach (var entry in scheme.AllEntries)
            {
                for (int i = 0; i < attributeCount; i++)
                {
                    var attribute = scheme.GetAttribute(i);
                    csvContent.Append(entry.GetData(attribute.AttributeName));
                    
                    // fence posting
                    if (i != attributeCount - 1)
                    {
                        csvContent.Append(",");
                    }
                }
                csvContent.AppendLine();
            }

            // Write to file
            fileSystem.WriteAllText(filePath, csvContent.ToString());
            
            return Pass($"Wrote {scheme} to file: {filePath}");
        }

        public string Serialize(DataScheme data)
        {
            throw new NotImplementedException();
        }
    }
}