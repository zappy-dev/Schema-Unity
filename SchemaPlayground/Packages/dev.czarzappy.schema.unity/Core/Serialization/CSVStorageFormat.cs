using System;
using System.Collections.Generic;
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
            return DeserializeFromFile(filePath).Try(out data);
        }

        public bool TryDeserialize(string content, out DataScheme data)
        {
            return Deserialize(content).Try(out data);
        }

        private readonly IFileSystem fileSystem;

        public CSVStorageFormat(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }
        
        public SchemaResult<DataScheme> DeserializeFromFile(string filePath)
        {
            var schemeName = Path.GetFileNameWithoutExtension(filePath);
            var rows = fileSystem.ReadAllLines(filePath);

            return LoadFromRows(schemeName, rows);
        }

        public static string[] SplitToRows(string content) => content.Split(new[]
        {
            Environment.NewLine,
        }, StringSplitOptions.None);

        public SchemaResult<DataScheme> Deserialize(string content)
        {
            string[] rows = SplitToRows(content);

            return LoadFromRows(schemeName: "unnamed", rows);
        }

        private SchemaResult<DataScheme> LoadFromRows(string schemeName, string[] rows)
        {
            if (rows.Length == 0)
            {
                return SchemaResult<DataScheme>.Fail("No rows were provided", this);
            }
            
            var importedScheme = new DataScheme(schemeName);
            var header = rows[0].Split(',');
            var attrCount = header.Length;
            
            var attributes = header.Select(h => new AttributeDefinition
            {
                AttributeName = h,
                DataType = DataType
                    .Text, // TODO: determine datatype, maybe scan entries or hint in header name, e.g header (type). Alternatively, use existing scheme's type info
                DefaultValue = string.Empty,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth,
            }).ToArray();

            // validate and extract out raw data
            var rawDataEntries = new List<string[]>();
            for (var rowIdx = 1; rowIdx < rows.Length; rowIdx++)
            {
                var row = rows[rowIdx];
                // skip empty rows / maybe last row
                if (string.IsNullOrWhiteSpace(row))
                {
                    continue;
                }
                var entries = row.Split(',');
                var numEntries = entries.Length;
                // skip empty rows / maybe last row
                if (numEntries == 0)
                {
                    continue;
                }
                if (numEntries != attrCount)
                {
                    return SchemaResult<DataScheme>.Fail($"Row {rowIdx}: Invalid number of rows, expected {attrCount}, found {numEntries}, row: {row}", this);
                }
                
                rawDataEntries.Add(entries);
            }
            
            // determine the best data type for a column
            for (var attrIdx = 0; attrIdx < attrCount; attrIdx++)
            {
                var potentialDataTypes = new HashSet<DataType>(DataType.BuiltInTypes);
                foreach (var dataEntry in rawDataEntries)
                {
                    var rawData = dataEntry[attrIdx];
                    var enumerator = potentialDataTypes.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        if (!enumerator.Current.ConvertData(rawData).Try(out var convertedData))
                        {
                            potentialDataTypes.Remove(enumerator.Current);
                        }
                    }
                }

                if (potentialDataTypes.Count == 0)
                {
                    return SchemaResult<DataScheme>.Fail($"Could not convert all entries for colunn {attrIdx} to a known data type", context: this);
                }

                DataType finalDataType;
                if (potentialDataTypes.Count >= 2)
                {
                    // prefer non-text data type if possible
                    finalDataType = potentialDataTypes.First(dataType => !dataType.Equals(DataType.Text));
                }
                else
                {
                    finalDataType = potentialDataTypes.First();
                }

                attributes[attrIdx].DataType = finalDataType;
            }
            
            // add attributes after resolving data type
            var failures = attributes.Select(a => importedScheme.AddAttribute(a))
                .Where(res => res.Failed);

            if (failures.Any())
            {
                // TODO: Improve CSV validation and error reporting
                throw new InvalidOperationException(string.Join(",", failures));
            }
            
            // TODO: Is there a way to do this without converting the data twice?
            // now parse data with the final data type
            foreach (var rawDataEntry in rawDataEntries)
            {
                var entry = new DataEntry();
                for (var attrIdx = 0; attrIdx < attrCount; attrIdx++)
                {
                    var rawData = rawDataEntry[attrIdx];
                    var dataType = attributes[attrIdx].DataType;
                    var attributeName = attributes[attrIdx].AttributeName;

                    if (!dataType.ConvertData(rawData).Try(out var convertedData))
                    {
                        return SchemaResult<DataScheme>.Fail($"Could not convert entry {rawDataEntry} to type {dataType}", context: this);
                    }

                    var setDataRes = entry.SetData(attributeName, convertedData);
                    if (setDataRes.Failed)
                    {
                        return SchemaResult<DataScheme>.Fail(setDataRes.Message, context: this);
                    }
                }
                
                var addRes = importedScheme.AddEntry(entry);
                if (addRes.Failed)
                {
                    return SchemaResult<DataScheme>.Fail(addRes.Message, context: this);
                }
            }

            return SchemaResult<DataScheme>.Pass(importedScheme,
                successMessage: "Loaded scheme",
                context: this);
        }

        public SchemaResult SerializeToFile(string filePath, DataScheme scheme)
        {
            if (scheme == null)
            {
                return Fail($"Scheme cannot be null", this);
            }

            if (!Serialize(scheme).Try(out var csvContent))
            {
                return Fail("Failed to deserialize scheme", this);
            }

            // Write to file
            fileSystem.WriteAllText(filePath, csvContent);
            
            return Pass($"Wrote {scheme} to file: {filePath}");
        }

        public SchemaResult<string> Serialize(DataScheme scheme)
        {
            StringBuilder csvContent = new StringBuilder();

            // Add headers
            int attributeCount = scheme.AttributeCount;
            if (attributeCount == 0)
            {
                // return null;
                return SchemaResult<string>.Fail($"Scheme cannot be empty", this);
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

            return SchemaResult<string>.Pass(csvContent.ToString(),
                successMessage: "Parsed CSV content",
                this);
        }
    }
}