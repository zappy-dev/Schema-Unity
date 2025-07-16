using System;
using Newtonsoft.Json;
using Schema.Core.Serialization;

namespace Schema.Core.Data
{
    [Serializable]
    public class FilePathDataType : DataType
    {
        [JsonProperty("AllowEmptyPath")]
        private bool allowEmptyPath;
        
        [JsonIgnore]
        public bool AllowEmptyPath => allowEmptyPath;
        public override string TypeName => "FilePath";

        public FilePathDataType(bool allowEmptyPath = true) : base(string.Empty)
        {
            this.allowEmptyPath = allowEmptyPath;
        }
        
        public override SchemaResult CheckIfValidData(object value)
        {
            if (!(value is string filePath))
            {
                return Fail($"Value '{value}' is not a file path");
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return CheckIf(allowEmptyPath, errorMessage: "File path is empty", successMessage: "File path is set");
            }
            
            return CheckIf(Serialization.Storage.FileSystem.FileExists(filePath), 
                errorMessage: $"File '{value}' does not exist",
                successMessage: $"File '{filePath}' exists");
        }

        public override SchemaResult<object> ConvertData(object value)
        {
            var data = value as string;
            
            return SchemaResult<object>.CheckIf(Serialization.Storage.FileSystem.FileExists(data), 
                result: data,
                errorMessage: $"File '{value}' is not a file path",
                successMessage: $"File '{value}' exists", 
                context: this);
        }
    }
}