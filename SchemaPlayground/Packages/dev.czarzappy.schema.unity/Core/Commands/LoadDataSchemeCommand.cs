#define SCHEMA_DEBUG
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Schema.Core.Data;
using Schema.Core.Logging;
using Schema.Core.Schemes;
using static Schema.Core.Commands.CommandResult;

namespace Schema.Core.Commands
{
    /// <summary>
    /// Command for loading a data scheme with full undo support
    /// </summary>
    public class LoadDataSchemeCommand : SchemaCommandBase
    {
        private readonly DataScheme _scheme;
        private readonly bool _overwriteExisting;
        private readonly string _importFilePath;
        private readonly IProgress<CommandProgress> _progress;

        // State for undo operations
        private DataScheme _previousScheme;
        private bool _schemeExistedBefore;
        private bool _wasExecuted;
        
        public override string Description => $"Load data scheme '{_scheme.SchemeName}'{(_importFilePath != null ? $" from '{_importFilePath}'" : "")}";
        
        public LoadDataSchemeCommand(
            SchemaContext context,
            DataScheme scheme, 
            bool overwriteExisting, 
            string importFilePath = null,
            IProgress<CommandProgress> progress = null) : base(context)
        {
            _scheme = scheme ?? throw new ArgumentNullException(nameof(scheme));
            _overwriteExisting = overwriteExisting;
            _importFilePath = importFilePath;
            _progress = progress;
        }
        
        protected override async Task<CommandResult> ExecuteInternalAsync(CancellationToken cancellationToken)
        {
            ReportProgress(_progress, 0.0f, "Starting scheme load...");
            
            // 1. Validate inputs
            if (string.IsNullOrWhiteSpace(_scheme.SchemeName))
            {
                return Fail("Schema name is invalid");
            }
            
            ReportProgress(_progress, 0.1f, "Validating scheme...");
            
            // 2. Check if scheme already exists and handle overwrite logic
            _schemeExistedBefore = Schema.DoesSchemeExist(_scheme.SchemeName);
            
            if (_schemeExistedBefore)
            {
                if (!_overwriteExisting)
                {
                    return Fail($"Schema '{_scheme.SchemeName}' already exists and overwrite is not enabled");
                }
                
                // Store the previous scheme for undo
                var previousSchemeResult = Schema.GetScheme(_scheme.SchemeName);
                if (previousSchemeResult.Try(out _previousScheme))
                {
                    Logger.LogDbgVerbose($"Stored previous scheme '{_previousScheme.SchemeName}' for undo", this);
                }
            }
            
            ReportProgress(_progress, 0.3f, "Processing scheme data...");
            
            // 3. Process and validate all entry data
            await ProcessSchemeDataAsync(_scheme, cancellationToken);
            
            ReportProgress(_progress, 0.7f, "Loading scheme into system...");
            
            // 4. Load the scheme into the system
            var loadResult = await LoadSchemeIntoSystemAsync(_scheme, cancellationToken);
            
            if (loadResult.IsFailure)
            {
                return loadResult;
            }
            
            ReportProgress(_progress, 0.9f, "Updating manifest...");
            
            // 5. Update manifest if necessary
            if (!string.IsNullOrWhiteSpace(_importFilePath))
            {
                await UpdateManifestAsync(_scheme, _importFilePath, cancellationToken);
            }
            
            _wasExecuted = true;
            ReportProgress(_progress, 1.0f, "Scheme loaded successfully");
            
            Logger.LogDbgVerbose($"Successfully loaded scheme '{_scheme.SchemeName}'", this);
            return Pass(_scheme, $"Successfully loaded scheme '{_scheme.SchemeName}'");
        }
        
        protected override async Task<CommandResult> UndoInternalAsync(CancellationToken cancellationToken)
        {
            if (!_wasExecuted)
            {
                return Fail("Cannot undo command that was not executed");
            }
            
            Logger.LogDbgVerbose($"Undoing load of scheme '{_scheme.SchemeName}'", this);
            
            try
            {
                if (_schemeExistedBefore && _previousScheme != null)
                {
                    // Restore the previous scheme
                    Logger.LogDbgVerbose($"Restoring previous scheme '{_previousScheme.SchemeName}'", this);
                    
                    // Use the synchronous method for now - this will be replaced when Schema interface is updated
                    var restoreResult = Schema.LoadDataScheme(Context, _previousScheme, overwriteExisting: true);
                    
                    if (restoreResult.Failed)
                    {
                        Logger.LogError(restoreResult.Message, restoreResult.Context);
                        return Fail($"Failed to restore previous scheme: {restoreResult.Message}");
                    }
                }
                else
                {
                    // Remove the scheme that was added
                    Logger.LogDbgVerbose($"Removing added scheme '{_scheme.SchemeName}'", this);
                    
                    // Remove the scheme using existing functionality
                    // Note: This is a temporary implementation - proper removal will be implemented
                    // when the Schema interface is fully converted to async
                    
                    // Update manifest to remove the entry
                    await RemoveFromManifestAsync(_scheme.SchemeName, cancellationToken);
                    await UnloadSchemeFromSystemAsync(_scheme.SchemeName, cancellationToken);
                }
                
                return Pass($"Successfully undone load of scheme '{_scheme.SchemeName}'");
            }
            catch (Exception ex)
            {
                Logger.LogDbgError($"Failed to undo load of scheme '{_scheme.SchemeName}': {ex.Message}", this);
                return Fail($"Failed to undo load: {ex.Message}", ex);
            }
        }

        private async Task ProcessSchemeDataAsync(DataScheme scheme, CancellationToken cancellationToken)
        {
            ThrowIfCancellationRequested(cancellationToken);
            
            // Process all entries asynchronously
            var entries = scheme.AllEntries;
            var attributes = scheme.GetAttributes();
            
            int processedEntries = 0;
            int totalEntries = entries.Count();
            
            foreach (var entry in entries)
            {
                ThrowIfCancellationRequested(cancellationToken);
                
                foreach (var attribute in attributes)
                {
                    ThrowIfCancellationRequested(cancellationToken);
                    
                    var entryData = entry.GetData(attribute);
                    if (entryData.Failed)
                    {
                        // Set default value
                        scheme.SetDataOnEntry(entry, attribute.AttributeName, attribute.CloneDefaultValue(), context: Context);
                    }
                    else
                    {
                        // Validate and potentially convert data
                        var fieldData = entryData.Result;
                        var validateResult = attribute.CheckIfValidData(Context, fieldData);
                        
                        if (validateResult.Failed && !scheme.IsManifest)
                        {
                            var conversionResult = attribute.ConvertData(Context, fieldData);
                            if (conversionResult.Failed)
                            {
                                // Allow file path types to load even if file doesn't exist
                                if (attribute.DataType != DataType.FilePath_RelativePaths &&
                                    attribute.DataType != DataType.Folder_RelativePaths)
                                {
                                    Fail(
                                        $"Failed to convert data for attribute '{attribute.AttributeName}': {conversionResult.Message}");
                                    return;
                                }
                            }
                            else
                            {
                                scheme.SetDataOnEntry(entry, attribute.AttributeName, conversionResult.Result, context: Context);
                            }
                        }
                    }
                }
                
                processedEntries++;
                if (totalEntries > 0)
                {
                    var progress = 0.3f + (0.4f * processedEntries / totalEntries);
                    ReportProgress(_progress, progress, $"Processed {processedEntries}/{totalEntries} entries");
                }
            }

            Pass(scheme);
        }
        
        private async Task<CommandResult> LoadSchemeIntoSystemAsync(DataScheme scheme, CancellationToken cancellationToken)
        {
            ThrowIfCancellationRequested(cancellationToken);
            
            // This is a temporary implementation - will be replaced when Schema interface is updated
            await Task.Run(() =>
            {
                scheme.SetDirty(Context, true);
                // Use the existing LoadDataScheme method until the Schema interface is fully converted
                var result = Schema.LoadDataScheme(Context, scheme, _overwriteExisting);
                if (result.Failed)
                {
                    Logger.LogError(result.Message, result.Context);
                    throw new InvalidOperationException($"Failed to load scheme: {result.Message}");
                }
                Logger.LogDbgVerbose(result.Message, result.Context);
            }, cancellationToken);
            
            return Pass(scheme);
        }

        private async Task UnloadSchemeFromSystemAsync(string schemeName, CancellationToken cancellationToken)
        {
            ThrowIfCancellationRequested(cancellationToken);
            
            // This is a temporary implementation - will be replaced when Schema interface is updated
            await Task.Run(() =>
            {
                var result = Schema.UnloadScheme(Context, schemeName);
                if (result.Failed)
                {
                    throw new InvalidOperationException($"Failed to unload scheme: {result.Message}");
                }
            }, cancellationToken);
        }
        
        private async Task UpdateManifestAsync(DataScheme scheme, string importFilePath, CancellationToken cancellationToken)
        {
            ThrowIfCancellationRequested(cancellationToken);
            
            // This is a simplified implementation - will be enhanced with proper manifest management
            await Task.Run(() =>
            {
                if (Schema.GetManifestScheme().Try(out var manifestScheme))
                {
                    // Add or update manifest entry
                    if (!manifestScheme.GetEntryForSchemeName(Context, scheme.SchemeName).Try(out var manifestEntry))
                    {
                        manifestScheme.AddManifestEntry(Context, scheme.SchemeName,
                            publishTarget: ManifestScheme.PublishTarget.DEFAULT).Try(out manifestEntry);
                    }
                    
                    // Get the FilePath attribute definition to access its DataType
                    if (manifestScheme._.GetAttribute(nameof(ManifestEntry.FilePath)).Try(out var filePathAttr) &&
                        filePathAttr.DataType is FilePathDataType)
                    {
                        // Let the FilePathDataType handle the path conversion
                        var convertResult = filePathAttr.ConvertData(Context, importFilePath);
                        if (convertResult.Try(out var convertedPath))
                        {
                            manifestEntry.FilePath = convertedPath as string;
                            // manifestScheme._.SetDataOnEntry(manifestEntry, nameof(ManifestEntry.FilePath), convertedPath);
                        }
                        else
                        {
                            // Fallback to direct path if conversion fails
                            manifestEntry.FilePath = importFilePath;
                            // manifestScheme._.SetDataOnEntry(manifestEntry, nameof(ManifestEntry.FilePath), importFilePath);
                        }
                    }
                    else
                    {
                        // Fallback if we can't get the attribute or it's not a FilePathDataType
                        manifestEntry.FilePath = importFilePath;
                    }
                    
                    manifestScheme.SetDirty(Context, true);
                }
            }, cancellationToken);
        }
        
        private async Task RemoveFromManifestAsync(string schemeName, CancellationToken cancellationToken)
        {
            ThrowIfCancellationRequested(cancellationToken);
            
            await Task.Run(() =>
            {
                if (Schema.GetManifestScheme().Try(out var manifestScheme))
                {
                    if (manifestScheme.GetEntryForSchemeName(Context, schemeName).Try(out var manifestEntry))
                    {
                        manifestScheme.DeleteEntry(Context, manifestEntry);
                        manifestScheme.SetDirty(Context, true);
                    }
                }
            }, cancellationToken);
        }
    }
}