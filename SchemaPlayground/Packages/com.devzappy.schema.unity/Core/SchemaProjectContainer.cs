using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Schema.Core.Data;
using Schema.Core.Schemes;

public class SchemaProjectContainer : IDisposable
{
    private readonly Dictionary<string, DataScheme> _loadedSchemes = new Dictionary<string, DataScheme>();
    
    private static ManifestScheme _loadedManifestScheme;
    public IReadOnlyDictionary<string, DataScheme> Schemes =>  _loadedSchemes;
    public ManifestScheme Manifest
    {
        get => _loadedManifestScheme;
        set => _loadedManifestScheme = value;
    }

    public void Dispose()
    {
        _loadedSchemes.Clear();
        _loadedManifestScheme = null;
    }

    #region API
    public void AddScheme(DataScheme scheme)
    {
        _loadedSchemes[scheme.SchemeName] = scheme;
    }

    public bool RemoveScheme(DataScheme scheme)
    {
        return RemoveScheme(scheme.SchemeName);
    }

    public bool RemoveScheme(string schemeName)
    {
        return _loadedSchemes.Remove(schemeName);
    }
    
    #endregion

    public bool HasScheme(string schemeName)
    {
        return  _loadedSchemes.ContainsKey(schemeName);
    }

    public DataScheme FindScheme(Func<DataScheme, bool> predicate)
    {
        return _loadedSchemes.Values.FirstOrDefault(predicate);
    }

    public override string ToString()
    {
        return $"ProjectContainer({RuntimeHelpers.GetHashCode(this)})";
    }
}