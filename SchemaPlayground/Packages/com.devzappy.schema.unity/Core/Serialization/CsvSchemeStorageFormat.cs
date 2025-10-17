using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Data;
using Schema.Core.IO;
using static Schema.Core.SchemaResult;

namespace Schema.Core.Serialization
{
    public class CsvSchemeStorageFormat : ISchemeStorageFormat
    {
        public string Extension => "csv";
        public string DisplayName => "CSV";
        public bool IsImportSupported => true;
        public bool IsExportSupported => true;

        private readonly IFileSystem fileSystem;

        public CsvSchemeStorageFormat(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }
        
        public async Task<SchemaResult<DataScheme>> DeserializeFromFile(SchemaContext context, string filePath,
            CancellationToken cancellationToken = default)
        {
            var schemeName = Path.GetFileNameWithoutExtension(filePath);
            var readLinesRes = await fileSystem.ReadAllLines(context, filePath, cancellationToken);
            if (!readLinesRes.Try(out var rows))
            {
                return readLinesRes.CastError<DataScheme>();
            }

            return LoadFromRows(context, schemeName, rows);
        }

        public static string[] SplitToRows(string content) => content.Split(new[]
        {
            Environment.NewLine,
        }, StringSplitOptions.None);

        public SchemaResult<DataScheme> Deserialize(SchemaContext context, string content)
        {
            string[] rows = SplitToRows(content);

            return LoadFromRows(context, schemeName: "unnamed", rows);
        }

        private SchemaResult<DataScheme> LoadFromRows(SchemaContext ctx, string schemeName, string[] rows)
        {
            if (rows.Length == 0)
            {
                return SchemaResult<DataScheme>.Fail("No rows were provided", this);
            }
            
            var importedScheme = new DataScheme(schemeName);
            using var _ = new SchemeContextScope(ref ctx, importedScheme);
            
            var header = rows[0].Split(',');
            var attrCount = header.Length;
            
            var attributes = header.Select(h => new AttributeDefinition
            {
                AttributeName = h,
                DataType = DataType
                    .Text,
                ColumnWidth = AttributeDefinition.DefaultColumnWidth,
                AttributeToolTip = string.Empty,
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
                using var _2 = new AttributeContextScope(ref ctx, attributes[attrIdx].AttributeName);

                var columnValues = rawDataEntries.Select(dataEntry => dataEntry[attrIdx]).ToArray();
                if (!DataType.InferDataTypeForValues(ctx, columnValues)
                        .Try(out var finalDataType, out var inferError)) return inferError.CastError<DataScheme>();

                attributes[attrIdx].DataType = finalDataType;
            }
            
            // add attributes after resolving data type
            var failures = attributes.Select(a => importedScheme.AddAttribute(ctx, a))
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
                    using var _3 = new AttributeContextScope(ref ctx, attributes[attrIdx].AttributeName);
                    
                    var rawData = rawDataEntry[attrIdx];
                    var dataType = attributes[attrIdx].DataType;
                    var attributeName = attributes[attrIdx].AttributeName;

                    if (!dataType.ConvertValue(ctx, rawData).Try(out var convertedData))
                    {
                        return SchemaResult<DataScheme>.Fail($"Could not convert entry {rawDataEntry} to type {dataType}", context: this);
                    }

                    var setDataRes = entry.SetData(ctx, attributeName, convertedData);
                    if (setDataRes.Failed)
                    {
                        return SchemaResult<DataScheme>.Fail(setDataRes.Message, context: this);
                    }
                }
                
                var addRes = importedScheme.AddEntry(ctx, entry);
                if (addRes.Failed)
                {
                    return SchemaResult<DataScheme>.Fail(addRes.Message, context: this);
                }
            }

            return SchemaResult<DataScheme>.Pass(importedScheme,
                successMessage: "Loaded scheme",
                context: this);
        }

        public async Task<SchemaResult> SerializeToFile(SchemaContext context, string filePath, DataScheme scheme,
            CancellationToken cancellationToken = default)
        {
            if (scheme == null)
            {
                return Fail(context, $"Scheme cannot be null");
            }

            if (!Serialize(context, scheme).Try(out var csvContent))
            {
                return Fail(context, "Failed to deserialize scheme");
            }

            // Write to file
            await fileSystem.WriteAllText(context, filePath, csvContent, cancellationToken);
            
            return Pass($"Wrote {scheme} to file: {filePath}");
        }

        public SchemaResult<string> Serialize(SchemaContext context, DataScheme scheme)
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
                if (!scheme.GetAttribute(i).Try(out var attribute, out var error))
                {
                    return error.CastError<string>();
                }
                
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
                    if (!scheme.GetAttribute(i).Try(out var attribute, out var error))
                    {
                        return error.CastError<string>();
                    }
                    
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