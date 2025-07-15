using NUnit.Framework;
using Schema.Core;
using Schema.Core.Data;
using Schema.Unity.Editor.Tests.Mocks;
using UnityEngine;
using System;

namespace Schema.Unity.Editor.Tests.Unit
{
    [TestFixture]
    public class SchemaEditorWindowUnitTests
    {
        private TestableSchemaEditorWindow testWindow;
        
        [SetUp]
        public void Setup()
        {
            testWindow = new TestableSchemaEditorWindow();
            MockEditorPrefs.Clear();
        }
        
        [TearDown]
        public void TearDown()
        {
            MockEditorPrefs.Clear();
            testWindow?.Reset();
        }
        
        #region Manifest Load Response Tests
        
        [Test]
        public void LatestManifestLoadResponse_WhenSetToSuccess_ShouldReturnCorrectValue()
        {
            // Arrange
            var expectedResponse = SchemaResult<ManifestLoadStatus>.Success(ManifestLoadStatus.Success);
            
            // Act
            testWindow.LatestManifestLoadResponse = expectedResponse;
            
            // Assert
            Assert.That(testWindow.LatestManifestLoadResponse.Status, Is.EqualTo(RequestStatus.Success));
            Assert.That(testWindow.LatestManifestLoadResponse.Passed, Is.True);
            Assert.That(testWindow.LatestManifestLoadResponse.Value, Is.EqualTo(ManifestLoadStatus.Success));
        }
        
        [Test]
        public void LatestManifestLoadResponse_WhenSetToFailed_ShouldReturnCorrectValue()
        {
            // Arrange
            var expectedResponse = SchemaResult<ManifestLoadStatus>.Failed("Test error message");
            
            // Act
            testWindow.LatestManifestLoadResponse = expectedResponse;
            
            // Assert
            Assert.That(testWindow.LatestManifestLoadResponse.Status, Is.EqualTo(RequestStatus.Failed));
            Assert.That(testWindow.LatestManifestLoadResponse.Passed, Is.False);
            Assert.That(testWindow.LatestManifestLoadResponse.Message, Is.EqualTo("Test error message"));
        }
        
        #endregion
        
        #region Response History Tests
        
        [Test]
        public void ResponseHistory_WhenEmpty_ShouldReturnZeroCount()
        {
            // Assert
            Assert.That(testWindow.ResponseHistoryCount, Is.EqualTo(0));
            Assert.That(testWindow.GetLatestResponse(), Is.Null);
        }
        
        [Test]
        public void ResponseHistory_WhenAddingMultipleResponses_ShouldMaintainOrder()
        {
            // Arrange
            var response1 = SchemaResult.Success("First response");
            var response2 = SchemaResult.Failed("Second response");
            var response3 = SchemaResult.Success("Third response");
            
            // Act
            testWindow.AddToResponseHistory(response1);
            testWindow.AddToResponseHistory(response2);
            testWindow.AddToResponseHistory(response3);
            
            // Assert
            Assert.That(testWindow.ResponseHistoryCount, Is.EqualTo(3));
            Assert.That(testWindow.GetLatestResponse().Message, Is.EqualTo("Third response"));
            
            var history = testWindow.GetResponseHistory();
            Assert.That(history[0].Message, Is.EqualTo("First response"));
            Assert.That(history[1].Message, Is.EqualTo("Second response"));
            Assert.That(history[2].Message, Is.EqualTo("Third response"));
        }
        
        [Test]
        public void ResponseHistory_WhenCleared_ShouldBeEmpty()
        {
            // Arrange
            testWindow.AddToResponseHistory(SchemaResult.Success("Test"));
            Assert.That(testWindow.ResponseHistoryCount, Is.EqualTo(1));
            
            // Act
            testWindow.ClearResponseHistory();
            
            // Assert
            Assert.That(testWindow.ResponseHistoryCount, Is.EqualTo(0));
            Assert.That(testWindow.GetLatestResponse(), Is.Null);
        }
        
        [Test]
        public void LatestResponseTime_WhenAddingResponse_ShouldUpdateTime()
        {
            // Arrange
            var beforeTime = DateTime.Now;
            
            // Act
            testWindow.AddToResponseHistory(SchemaResult.Success("Test"));
            
            // Assert
            Assert.That(testWindow.LatestResponseTime, Is.GreaterThanOrEqualTo(beforeTime));
            Assert.That(testWindow.LatestResponseTime, Is.LessThanOrEqualTo(DateTime.Now));
        }
        
        #endregion
        
        #region Schema Selection Tests
        
        [Test]
        public void SelectScheme_WhenCalled_ShouldUpdateStateAndEditorPrefs()
        {
            // Arrange
            var schemeName = "TestSchema";
            
            // Act
            testWindow.SelectScheme(schemeName);
            
            // Assert
            Assert.That(testWindow.SelectedSchemeName, Is.EqualTo(schemeName));
            Assert.That(testWindow.SelectedSchemaIndex, Is.EqualTo(0)); // Mock returns 0 for non-empty names
            Assert.That(MockEditorPrefs.GetString("Schema:SelectedSchemeName"), Is.EqualTo(schemeName));
        }
        
        [Test]
        public void SelectScheme_WithEmptyName_ShouldResetSelection()
        {
            // Arrange
            testWindow.SelectScheme("SomeSchema");
            
            // Act
            testWindow.SelectScheme("");
            
            // Assert
            Assert.That(testWindow.SelectedSchemeName, Is.EqualTo(""));
            Assert.That(testWindow.SelectedSchemaIndex, Is.EqualTo(-1));
            Assert.That(MockEditorPrefs.GetString("Schema:SelectedSchemeName"), Is.EqualTo(""));
        }
        
        [Test]
        public void SelectScheme_WithNullName_ShouldResetSelection()
        {
            // Arrange
            testWindow.SelectScheme("SomeSchema");
            
            // Act
            testWindow.SelectScheme(null);
            
            // Assert
            Assert.That(testWindow.SelectedSchemeName, Is.EqualTo(null));
            Assert.That(testWindow.SelectedSchemaIndex, Is.EqualTo(-1));
            Assert.That(MockEditorPrefs.GetString("Schema:SelectedSchemeName"), Is.EqualTo(null));
        }
        
        #endregion
        
        #region Initialization Tests
        
        [Test]
        public void IsInitialized_WhenToggled_ShouldReturnCorrectState()
        {
            // Arrange
            Assert.That(testWindow.IsInitialized, Is.False);
            
            // Act
            testWindow.SetInitialized(true);
            
            // Assert
            Assert.That(testWindow.IsInitialized, Is.True);
            
            // Act again
            testWindow.SetInitialized(false);
            
            // Assert
            Assert.That(testWindow.IsInitialized, Is.False);
        }
        
        [Test]
        public void SimulateInitialization_ShouldSetExpectedValues()
        {
            // Act
            testWindow.SimulateInitialization();
            
            // Assert
            Assert.That(testWindow.IsInitialized, Is.True);
            Assert.That(testWindow.ManifestFilePath, Is.EqualTo("Content/Manifest.json"));
            Assert.That(testWindow.TooltipOfTheDay, Is.EqualTo("Test tooltip of the day"));
            Assert.That(testWindow.ResponseHistoryCount, Is.EqualTo(1));
            Assert.That(testWindow.LatestManifestLoadResponse.Passed, Is.True);
        }
        
        #endregion
        
        #region UI State Tests
        
        [Test]
        public void ToggleDebugView_WhenCalled_ShouldToggleState()
        {
            // Arrange
            Assert.That(testWindow.ShowDebugView, Is.False);
            
            // Act
            testWindow.ToggleDebugView();
            
            // Assert
            Assert.That(testWindow.ShowDebugView, Is.True);
            
            // Act again
            testWindow.ToggleDebugView();
            
            // Assert
            Assert.That(testWindow.ShowDebugView, Is.False);
        }
        
        [Test]
        public void ScrollPositions_WhenSet_ShouldRetainValues()
        {
            // Arrange
            var explorerPos = new Vector2(10, 20);
            var tablePos = new Vector2(30, 40);
            
            // Act
            testWindow.SetExplorerScrollPosition(explorerPos);
            testWindow.SetTableViewScrollPosition(tablePos);
            
            // Assert
            Assert.That(testWindow.ExplorerScrollPosition, Is.EqualTo(explorerPos));
            Assert.That(testWindow.TableViewScrollPosition, Is.EqualTo(tablePos));
        }
        
        [Test]
        public void NewSchemaName_WhenSet_ShouldRetainValue()
        {
            // Arrange
            var newName = "NewTestSchema";
            
            // Act
            testWindow.SetNewSchemeName(newName);
            
            // Assert
            Assert.That(testWindow.NewSchemeName, Is.EqualTo(newName));
        }
        
        [Test]
        public void NewAttributeName_WhenSet_ShouldRetainValue()
        {
            // Arrange
            var newAttrName = "NewTestAttribute";
            
            // Act
            testWindow.SetNewAttributeName(newAttrName);
            
            // Assert
            Assert.That(testWindow.NewAttributeName, Is.EqualTo(newAttrName));
        }
        
        #endregion
        
        #region Simulation Tests
        
        [Test]
        public void SimulateManifestLoad_WithSuccess_ShouldUpdateResponse()
        {
            // Act
            testWindow.SimulateManifestLoad(ManifestLoadStatus.Success, "Load successful");
            
            // Assert
            Assert.That(testWindow.LatestManifestLoadResponse.Passed, Is.True);
            Assert.That(testWindow.LatestManifestLoadResponse.Value, Is.EqualTo(ManifestLoadStatus.Success));
            Assert.That(testWindow.ResponseHistoryCount, Is.EqualTo(1));
        }
        
        [Test]
        public void SimulateManifestLoad_WithFailure_ShouldUpdateResponse()
        {
            // Act
            testWindow.SimulateManifestLoad(ManifestLoadStatus.FileNotFound, "File not found");
            
            // Assert
            Assert.That(testWindow.LatestManifestLoadResponse.Passed, Is.False);
            Assert.That(testWindow.LatestManifestLoadResponse.Message, Is.EqualTo("File not found"));
            Assert.That(testWindow.ResponseHistoryCount, Is.EqualTo(1));
        }
        
        [Test]
        public void SimulateAddSchema_WithSuccess_ShouldAddSuccessResponse()
        {
            // Arrange
            var schemaName = "TestSchema";
            
            // Act
            testWindow.SimulateAddSchema(schemaName, true);
            
            // Assert
            var latestResponse = testWindow.GetLatestResponse();
            Assert.That(latestResponse.Passed, Is.True);
            Assert.That(latestResponse.Message, Does.Contain(schemaName));
            Assert.That(latestResponse.Message, Does.Contain("added successfully"));
        }
        
        [Test]
        public void SimulateAddSchema_WithFailure_ShouldAddFailureResponse()
        {
            // Arrange
            var schemaName = "TestSchema";
            
            // Act
            testWindow.SimulateAddSchema(schemaName, false);
            
            // Assert
            var latestResponse = testWindow.GetLatestResponse();
            Assert.That(latestResponse.Passed, Is.False);
            Assert.That(latestResponse.Message, Does.Contain(schemaName));
            Assert.That(latestResponse.Message, Does.Contain("Failed to add"));
        }
        
        #endregion
        
        #region Reset Tests
        
        [Test]
        public void Reset_ShouldClearAllState()
        {
            // Arrange - Set up some state
            testWindow.SimulateInitialization();
            testWindow.SelectScheme("TestSchema");
            testWindow.SetNewSchemeName("NewSchema");
            testWindow.SetNewAttributeName("NewAttribute");
            testWindow.ToggleDebugView();
            testWindow.SetExplorerScrollPosition(new Vector2(100, 200));
            
            // Verify state is set
            Assert.That(testWindow.IsInitialized, Is.True);
            Assert.That(testWindow.ResponseHistoryCount, Is.GreaterThan(0));
            
            // Act
            testWindow.Reset();
            
            // Assert
            Assert.That(testWindow.IsInitialized, Is.False);
            Assert.That(testWindow.ResponseHistoryCount, Is.EqualTo(0));
            Assert.That(testWindow.SelectedSchemeName, Is.EqualTo(string.Empty));
            Assert.That(testWindow.NewSchemeName, Is.EqualTo(string.Empty));
            Assert.That(testWindow.NewAttributeName, Is.EqualTo(string.Empty));
            Assert.That(testWindow.SelectedSchemaIndex, Is.EqualTo(-1));
            Assert.That(testWindow.ShowDebugView, Is.False);
            Assert.That(testWindow.ExplorerScrollPosition, Is.EqualTo(Vector2.zero));
            Assert.That(testWindow.TableViewScrollPosition, Is.EqualTo(Vector2.zero));
            Assert.That(testWindow.ManifestFilePath, Is.EqualTo(string.Empty));
            Assert.That(testWindow.TooltipOfTheDay, Is.EqualTo(string.Empty));
        }
        
        #endregion
    }
}