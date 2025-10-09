using System;
using System.Collections.Generic;
using System.Linq;

namespace Schema.Core.Data
{
    public class PluginDataType : DataType
    {
        private readonly string _pluginTypeName;
        public string PluginTypeName => _pluginTypeName;
        private readonly Dictionary<string, object> _pluginData;
        public IReadOnlyDictionary<string, object> PluginData => _pluginData;
        
        public override SchemaResult<string> GetDataMethod(SchemaContext context, AttributeDefinition attribute) => throw new NotImplementedException();
        public override string CSDataType => throw new NotImplementedException();

        public PluginDataType(string pluginTypeName, Dictionary<string, object> pluginData)
        {
            _pluginTypeName = pluginTypeName;
            _pluginData = pluginData;
        }

        public override string TypeName => $"Plugin Data Type: {_pluginTypeName}";
        public override object Clone()
        {
            return new PluginDataType(_pluginTypeName.Clone() as string, _pluginData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        public override SchemaResult IsValidValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this.TypeName);
            return Fail("Unable to validate value as Plugin Data Type", context);
        }

        public override SchemaResult<object> ConvertValue(SchemaContext context, object value)
        {
            using var _ = new DataTypeContextScope(ref context, this.TypeName);
            return SchemaResult<object>.Fail("Unable to convert value to Plugin Data Type", context);
        }
    }
}