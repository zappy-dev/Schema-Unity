using System;
using System.Collections.Generic;
using System.Linq;
using Schema.Core;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor.Tests.Mocks
{
    /// <summary>
    /// Testable wrapper class that exposes internal functionality of SchemaEditorWindow for testing
    /// </summary>
    public class TestableSchemaEditorWindow
    {
        #region Private Fields (mirroring SchemaEditorWindow)
        
        private List<SchemaResult> responseHistory = new List<SchemaResult>();
        private string selectedSchemeName = string.Empty;
        private string newSchemeName = string.Empty;
        private string newAttributeName = string.Empty;
        private int selectedSchemaIndex = -1;
        private SchemaResult<ManifestLoadStatus> latestManifestLoadResponse;
        private bool isInitialized = false;
        private bool showDebugView = false;
        private Vector2 explorerScrollPosition = Vector2.zero;
        private Vector2 tableViewScrollPosition = Vector2.zero;
        private string manifestFilePath = string.Empty;
        private string tooltipOfTheDay = string.Empty;
        private DateTime latestResponseTime = DateTime.MinValue;
        
        #endregion
        
        #region Public Properties for Testing
        
        public SchemaResult<ManifestLoadStatus> LatestManifestLoadResponse
        {
            get => latestManifestLoadResponse;
            set => latestManifestLoadResponse = value;
        }
        
        public string SelectedSchemeName => selectedSchemeName;
        public string NewSchemeName => newSchemeName;
        public string NewAttributeName => newAttributeName;
        public int SelectedSchemaIndex => selectedSchemaIndex;
        public bool IsInitialized => isInitialized;
        public bool ShowDebugView => showDebugView;
        public Vector2 ExplorerScrollPosition => explorerScrollPosition;
        public Vector2 TableViewScrollPosition => tableViewScrollPosition;
        public string ManifestFilePath => manifestFilePath;
        public string TooltipOfTheDay => tooltipOfTheDay;
        public int ResponseHistoryCount => responseHistory.Count;
        public DateTime LatestResponseTime => latestResponseTime;
        
        #endregion
        
        #region Test Helper Methods
        
        public void AddToResponseHistory(SchemaResult result)
        {
            responseHistory.Add(result);
            latestResponseTime = DateTime.Now;
        }
        
        public SchemaResult GetLatestResponse()
        {
            return responseHistory.LastOrDefault();
        }
        
        public List<SchemaResult> GetResponseHistory()
        {
            return new List<SchemaResult>(responseHistory);
        }
        
        public void ClearResponseHistory()
        {
            responseHistory.Clear();
            latestResponseTime = DateTime.MinValue;
        }
        
        public void SelectScheme(string schemeName)
        {
            selectedSchemeName = schemeName;
            selectedSchemaIndex = GetSchemeIndex(schemeName);
            MockEditorPrefs.SetString("Schema:SelectedSchemeName", schemeName);
        }
        
        public void SetNewSchemeName(string name)
        {
            newSchemeName = name;
        }
        
        public void SetNewAttributeName(string name)
        {
            newAttributeName = name;
        }
        
        public void SetInitialized(bool value)
        {
            isInitialized = value;
        }
        
        public void ToggleDebugView()
        {
            showDebugView = !showDebugView;
        }
        
        public void SetExplorerScrollPosition(Vector2 position)
        {
            explorerScrollPosition = position;
        }
        
        public void SetTableViewScrollPosition(Vector2 position)
        {
            tableViewScrollPosition = position;
        }
        
        public void SetManifestFilePath(string path)
        {
            manifestFilePath = path;
        }
        
        public void SetTooltipOfTheDay(string tooltip)
        {
            tooltipOfTheDay = tooltip;
        }
        
        #endregion
        
        #region Mock Implementation Methods
        
        private int GetSchemeIndex(string schemeName)
        {
            // Mock implementation - in real code this would search the loaded schemes
            return string.IsNullOrEmpty(schemeName) ? -1 : 0;
        }
        
        /// <summary>
        /// Simulates loading a manifest and updating the response
        /// </summary>
        public void SimulateManifestLoad(ManifestLoadStatus status, string message = "")
        {
            if (status == ManifestLoadStatus.Success)
            {
                latestManifestLoadResponse = SchemaResult<ManifestLoadStatus>.Success(status);
            }
            else
            {
                latestManifestLoadResponse = SchemaResult<ManifestLoadStatus>.Failed(message);
            }
            
            AddToResponseHistory(latestManifestLoadResponse);
        }
        
        /// <summary>
        /// Simulates adding a new schema
        /// </summary>
        public void SimulateAddSchema(string schemaName, bool success = true)
        {
            if (success)
            {
                AddToResponseHistory(SchemaResult.Success($"Schema '{schemaName}' added successfully"));
            }
            else
            {
                AddToResponseHistory(SchemaResult.Failed($"Failed to add schema '{schemaName}'"));
            }
        }
        
        /// <summary>
        /// Simulates initialization process
        /// </summary>
        public void SimulateInitialization()
        {
            isInitialized = true;
            manifestFilePath = "Content/Manifest.json";
            tooltipOfTheDay = "Test tooltip of the day";
            SimulateManifestLoad(ManifestLoadStatus.Success, "Initialization complete");
        }
        
        /// <summary>
        /// Resets all state to initial values for testing
        /// </summary>
        public void Reset()
        {
            responseHistory.Clear();
            selectedSchemeName = string.Empty;
            newSchemeName = string.Empty;
            newAttributeName = string.Empty;
            selectedSchemaIndex = -1;
            latestManifestLoadResponse = default;
            isInitialized = false;
            showDebugView = false;
            explorerScrollPosition = Vector2.zero;
            tableViewScrollPosition = Vector2.zero;
            manifestFilePath = string.Empty;
            tooltipOfTheDay = string.Empty;
            latestResponseTime = DateTime.MinValue;
        }
        
        #endregion
    }
}