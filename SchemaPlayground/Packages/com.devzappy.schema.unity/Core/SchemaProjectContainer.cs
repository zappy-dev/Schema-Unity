using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Schema.Core.Data;
using Schema.Core.IO;
using Schema.Core.Logging;
using Schema.Core.Schemes;
using static Schema.Core.SchemaResult;

namespace Schema.Core
{
    public class SchemaProjectContainer : IDisposable
    {
        #region Fields and Properties

        private readonly Dictionary<string, DataScheme> _loadedSchemes = new Dictionary<string, DataScheme>();
    
        private static ManifestScheme _loadedManifestScheme;
        public IReadOnlyDictionary<string, DataScheme> Schemes =>  _loadedSchemes;
        public ManifestScheme Manifest
        {
            get => _loadedManifestScheme;
            set => _loadedManifestScheme = value;
        }
    
        private string manifestImportPath;
    
        /// <summary>
        /// The absolute file path from where the Manifest scheme was loaded from.
        /// This should only exist when a Manifest scheme was loaded into memory.
        /// </summary>
        public string ManifestImportPath
        {
            get => manifestImportPath;
        }

        public SchemaResult SetManifestImportPath(SchemaContext ctx, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Fail(ctx, "Manifest import path cannot be null or empty.");
            }

            // No-op
            if (manifestImportPath == path)
            {
                return Pass();
            }

            if (!string.IsNullOrWhiteSpace(manifestImportPath))
            {
                return Fail(ctx,$"Attempt to set Manifest import path is already set to {manifestImportPath}, new value: {path}");
            }
            
            Logger.LogDbgVerbose($"Manifest import path set to {path}", this);
            manifestImportPath = path;
            return Pass();
        }

        public string ProjectPath { get; set; }
    
        #endregion

        public void Dispose()
        {
            _loadedSchemes.Clear();
            _loadedManifestScheme = null;
        }

        #region Equality Members

        protected bool Equals(SchemaProjectContainer other)
        {
            return Equals(_loadedSchemes, other._loadedSchemes) && manifestImportPath == other.manifestImportPath && ProjectPath == other.ProjectPath;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SchemaProjectContainer)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (_loadedSchemes != null ? _loadedSchemes.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (manifestImportPath != null ? manifestImportPath.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ProjectPath != null ? ProjectPath.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion

        #region API

        public string ProjectRelativeManifestLoadPath =>
            PathUtility.MakeRelativePath(ManifestImportPath, ProjectPath);
    
        public string DefaultContentPath => Path.Combine(ProjectPath, global::Schema.Core.Schema.DefaultContentDirectory);

        public SchemaResult IsInitialized(SchemaContext ctx)
        {
            // this requirement isn't true during the runtime...?
            if (ctx.RuntimeType != SchemaRuntimeType.RUNTIME)
            {
                if (string.IsNullOrWhiteSpace(ProjectPath))
                {
                    return Fail(ctx,"Project path cannot be null or empty.");
                }
            }

            return CheckIf(ctx, Manifest != null, "Manifest not initialized.");
        }
    
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
            return $"ProjectContainer({RuntimeHelpers.GetHashCode(this)}) projectPath: {ProjectPath}, manifestImportPath: {ManifestImportPath}";
        }
    }
}