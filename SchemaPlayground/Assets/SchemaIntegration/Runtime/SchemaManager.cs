using System.IO;
using Schema.Core;
using Schema.Core.Serialization;
using UnityEngine;

namespace Schema.Unity
{
    public class SchemaManager
    {
        private string _dataPath = Application.dataPath + "/Resources/Schemes/";

        private readonly IStorageFormat<DataScheme> devStorageFormat;

        // Load a scheme from disk
        public DataScheme LoadScheme(string schemeName)
        {
            string filePath = _dataPath + schemeName + devStorageFormat.Extension;
            if (File.Exists(filePath))
            {
                return devStorageFormat.Load(filePath);
            }

            Debug.LogError("Scheme file not found!");
            return null;
        }

        // Save a scheme to disk
        public void SaveScheme(DataScheme scheme)
        {
            string filePath = _dataPath + scheme.SchemaName + devStorageFormat.Extension;
            devStorageFormat.Save(filePath, scheme);
        }
    }
}

