using Schema.Core.Serialization;

namespace Schema.Core.Data
{
    public class FilePathDataType : DataType
    {
        public override string TypeName => "FilePath";
        public override bool IsValid(object value)
        {
            return Storage.FileSystem.FileExists(value as string);
        }

        public FilePathDataType() : base(string.Empty)
        {
            
        }
    }
}