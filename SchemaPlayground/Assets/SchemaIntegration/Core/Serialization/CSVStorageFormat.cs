namespace Schema.Core
{
    public class CSVStorageFormat : IStorageFormat<DataScheme>
    {
        public string Extension => ".csv";
        public DataScheme Load(string filePath)
        {
            throw new System.NotImplementedException();
        }

        public void Save(string filePath, DataScheme data)
        {
            throw new System.NotImplementedException();
        }
    }
}