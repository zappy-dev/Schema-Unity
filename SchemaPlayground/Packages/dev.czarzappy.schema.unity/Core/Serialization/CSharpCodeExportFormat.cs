using System;
using System.IO;
using System.Text;
using Schema.Core.Data;
using Schema.Core.IO;

namespace Schema.Core.Serialization
{
    public class CSharpCodeExportFormat : IStorageFormat<DataScheme>
    {
        public string Extension => "cs";
        private readonly IFileSystem fileSystem;

        public CSharpCodeExportFormat(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public SchemaResult<DataScheme> DeserializeFromFile(string filePath)
        {
            throw new NotImplementedException();
        }

        public SchemaResult<DataScheme> Deserialize(string content)
        {
            throw new NotImplementedException();
        }

        public SchemaResult SerializeToFile(string filePath, DataScheme scheme)
        {
            if (!Serialize(scheme).Try(out var code))
                return SchemaResult.Fail("Failed to generate C# code", this);
            fileSystem.WriteAllText(filePath, code);
            return SchemaResult.Pass($"Wrote {scheme.SchemeName} C# class to file: {filePath}");
        }

        public SchemaResult<string> Serialize(DataScheme scheme)
        {
            var sb = new StringBuilder();
            var className = scheme.SchemeName;
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"public class {className}");
            sb.AppendLine("{");
            foreach (var attr in scheme.GetAttributes())
            {
                string type = MapDataTypeToCSharp(attr.DataType);
                string name = attr.AttributeName;
                sb.AppendLine($"    public {type} {name} {{ get; set; }}");
            }
            sb.AppendLine("}");
            return SchemaResult<string>.Pass(sb.ToString(), successMessage: $"Generated C# class for {className}", context: this);
        }

        private string MapDataTypeToCSharp(DataType dataType)
        {
            if (dataType is TextDataType) return "string";
            if (dataType is IntegerDataType) return "int";
            if (dataType is DateTimeDataType) return "DateTime";
            if (dataType is FilePathDataType) return "string";
            if (dataType is BooleanDataType) return "bool";
            if (dataType is ReferenceDataType) return "string"; // Could be improved
            return "string";
        }
    }
} 