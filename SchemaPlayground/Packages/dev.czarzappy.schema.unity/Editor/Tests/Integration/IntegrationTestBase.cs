using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Schema.Unity.Editor;

namespace Schema.Unity.Editor.Tests.Integration
{
    /// <summary>
    /// Base class for integration tests that require Unity Editor windows
    /// Handles common setup and cleanup operations
    /// </summary>
    [TestFixture]
    public abstract class IntegrationTestBase
    {
        protected SchemaEditorWindow window;
        
        [SetUp]
        public virtual void Setup()
        {
            // Clean up any existing windows before starting each test
            CleanupExistingWindows();
        }
        
        [TearDown]
        public virtual void TearDown()
        {
            // Clean up windows after each test to prevent interference
            CleanupExistingWindows();
        }
        
        /// <summary>
        /// Closes and cleans up all existing Schema editor windows and prompts
        /// </summary>
        protected void CleanupExistingWindows()
        {
            // Clean up SchemaEditorWindows
            var existingWindows = Resources.FindObjectsOfTypeAll<SchemaEditorWindow>();
            foreach (var w in existingWindows)
            {
                if (w != null)
                {
                    try
                    {
                        w.Close();
                    }
                    catch (System.Exception)
                    {
                        // Ignore exceptions during cleanup
                    }
                }
            }
            
            // Clean up AttributeSettingsPrompts
            var existingPrompts = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            foreach (var p in existingPrompts)
            {
                if (p != null)
                {
                    try
                    {
                        p.Close();
                    }
                    catch (System.Exception)
                    {
                        // Ignore exceptions during cleanup
                    }
                }
            }
            
            // Force Unity to update and process the window closures
            EditorApplication.QueuePlayerLoopUpdate();
        }
        
        /// <summary>
        /// Creates a new SchemaEditorWindow for testing
        /// </summary>
        /// <returns>A new SchemaEditorWindow instance</returns>
        protected SchemaEditorWindow CreateSchemaEditorWindow()
        {
            return EditorWindow.GetWindow<SchemaEditorWindow>();
        }
        
        /// <summary>
        /// Forces the window to update and repaint
        /// </summary>
        protected void WaitForWindowUpdate()
        {
            if (window != null)
            {
                window.Repaint();
                // Force immediate repaint
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }
        
        /// <summary>
        /// Creates a test window with a specific size and position
        /// </summary>
        /// <param name="rect">The desired window rectangle</param>
        /// <returns>A positioned SchemaEditorWindow</returns>
        protected SchemaEditorWindow CreatePositionedWindow(Rect rect)
        {
            var testWindow = CreateSchemaEditorWindow();
            testWindow.position = rect;
            return testWindow;
        }
        
        /// <summary>
        /// Waits for a specified number of editor frames
        /// Useful for waiting for UI updates to complete
        /// </summary>
        /// <param name="frames">Number of frames to wait</param>
        protected void WaitFrames(int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }
        
        /// <summary>
        /// Asserts that no exceptions are thrown during window operations
        /// </summary>
        /// <param name="operation">The operation to test</param>
        /// <param name="message">Optional custom error message</param>
        protected void AssertNoExceptionDuringOperation(System.Action operation, string message = "Operation should not throw exceptions")
        {
            Assert.DoesNotThrow(() => {
                operation?.Invoke();
            }, message);
        }
        
        /// <summary>
        /// Verifies that a window exists and is valid
        /// </summary>
        /// <param name="testWindow">The window to verify</param>
        protected void AssertWindowIsValid(EditorWindow testWindow)
        {
            Assert.That(testWindow, Is.Not.Null, "Window should not be null");
            Assert.That(testWindow.titleContent, Is.Not.Null, "Window title content should not be null");
        }
    }
}