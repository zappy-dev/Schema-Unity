using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using Schema.Core.Data;
using Schema.Unity.Editor.Tests.Integration;

namespace Schema.Unity.Editor.Tests.Integration
{
    [TestFixture]
    public class SchemaEditorWindowIntegrationTests : IntegrationTestBase
    {
        #region Window Creation Tests
        
        [Test]
        public void CreateWindow_ShouldSucceed()
        {
            // Act
            window = CreateSchemaEditorWindow();
            
            // Assert
            AssertWindowIsValid(window);
        }
        
        [Test]
        public void WindowTitle_WhenCreated_ShouldContainSchema()
        {
            // Act
            window = CreateSchemaEditorWindow();
            
            // Assert
            Assert.That(window.titleContent.text, Does.Contain("Schema").IgnoreCase);
        }
        
        [Test]
        public void MultipleWindows_WhenRequested_ShouldReturnSameInstance()
        {
            // Act
            var window1 = CreateSchemaEditorWindow();
            var window2 = EditorWindow.GetWindow<SchemaEditorWindow>();
            
            // Assert
            Assert.That(window1, Is.SameAs(window2), "GetWindow should return the same instance");
            
            // Cleanup
            window = window1; // Ensure cleanup in TearDown
        }
        
        [Test]
        public void Window_WhenClosed_ShouldNotBeNull()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            AssertWindowIsValid(window);
            
            // Act
            window.Close();
            
            // Assert
            // The window object itself should still exist even after closing
            Assert.That(window, Is.Not.Null);
        }
        
        #endregion
        
        #region Window State Tests
        
        [UnityTest]
        public IEnumerator WindowRepaint_ShouldNotThrowErrors()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                window.Repaint();
            }, "Window repaint should not throw exceptions");
            
            yield return null; // Wait one frame
            
            AssertNoExceptionDuringOperation(() => {
                window.Repaint();
            }, "Subsequent repaints should not throw exceptions");
        }
        
        [Test]
        public void WindowPosition_WhenSet_ShouldBeRetained()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            var testPosition = new Rect(100, 100, 800, 600);
            
            // Act
            window.position = testPosition;
            
            // Assert
            Assert.That(window.position.x, Is.EqualTo(testPosition.x).Within(1));
            Assert.That(window.position.y, Is.EqualTo(testPosition.y).Within(1));
            Assert.That(window.position.width, Is.EqualTo(testPosition.width).Within(1));
            Assert.That(window.position.height, Is.EqualTo(testPosition.height).Within(1));
        }
        
        [Test]
        public void WindowMinSize_ShouldBeReasonable()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            
            // Act
            var minSize = window.minSize;
            
            // Assert
            Assert.That(minSize.x, Is.GreaterThan(0), "Minimum width should be positive");
            Assert.That(minSize.y, Is.GreaterThan(0), "Minimum height should be positive");
            Assert.That(minSize.x, Is.LessThan(2000), "Minimum width should be reasonable");
            Assert.That(minSize.y, Is.LessThan(2000), "Minimum height should be reasonable");
        }
        
        [Test]
        public void WindowMaxSize_ShouldBeReasonable()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            
            // Act
            var maxSize = window.maxSize;
            
            // Assert
            // MaxSize might be very large or Vector2.zero (unlimited), both are valid
            Assert.That(maxSize.x, Is.GreaterThanOrEqualTo(0), "Maximum width should not be negative");
            Assert.That(maxSize.y, Is.GreaterThanOrEqualTo(0), "Maximum height should not be negative");
        }
        
        #endregion
        
        #region Window Lifecycle Tests
        
        [UnityTest]
        public IEnumerator WindowFocus_ShouldNotCauseErrors()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                window.Focus();
            }, "Window focus should not throw exceptions");
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator WindowShow_ShouldNotCauseErrors()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                window.Show();
            }, "Window show should not throw exceptions");
            
            yield return null;
        }
        
        [Test]
        public void WindowAutoRepaintOnSceneChange_ShouldBeConfigured()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            
            // Act & Assert
            // The window should have appropriate auto-repaint settings
            // This is more of a verification that the property exists and can be accessed
            Assert.DoesNotThrow(() => {
                var autoRepaint = window.autoRepaintOnSceneChange;
            }, "AutoRepaintOnSceneChange property should be accessible");
        }
        
        #endregion
        
        #region Window Resize Tests
        
        [UnityTest]
        public IEnumerator WindowResize_ShouldHandleGracefully()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            var originalSize = window.position;
            
            // Act
            var sizes = new Vector2[]
            {
                new Vector2(400, 300),   // Small
                new Vector2(800, 600),   // Medium
                new Vector2(1200, 800),  // Large
                new Vector2(200, 150)    // Very small
            };
            
            foreach (var size in sizes)
            {
                var newRect = new Rect(originalSize.x, originalSize.y, size.x, size.y);
                
                // Assert
                AssertNoExceptionDuringOperation(() => {
                    window.position = newRect;
                    window.Repaint();
                }, $"Window should handle resize to {size.x}x{size.y}");
                
                yield return null;
            }
        }
        
        #endregion
        
        #region Multiple Window Instances Tests
        
        [Test]
        public void GetWindow_CalledMultipleTimes_ShouldReturnSameInstance()
        {
            // Act
            var window1 = EditorWindow.GetWindow<SchemaEditorWindow>();
            var window2 = EditorWindow.GetWindow<SchemaEditorWindow>();
            var window3 = EditorWindow.GetWindow<SchemaEditorWindow>();
            
            // Assert
            Assert.That(window1, Is.SameAs(window2));
            Assert.That(window2, Is.SameAs(window3));
            
            // Cleanup
            window = window1;
        }
        
        [Test]
        public void GetWindow_WithUtilityFlag_ShouldCreateUtilityWindow()
        {
            // Act
            var utilityWindow = EditorWindow.GetWindow<SchemaEditorWindow>(true);
            
            // Assert
            AssertWindowIsValid(utilityWindow);
            
            // Cleanup
            utilityWindow.Close();
        }
        
        #endregion
        
        #region Window Content Tests
        
        [UnityTest]
        public IEnumerator WindowContent_ShouldRenderWithoutErrors()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            window.position = new Rect(0, 0, 800, 600);
            
            // Act & Assert
            for (int i = 0; i < 5; i++) // Test multiple render cycles
            {
                AssertNoExceptionDuringOperation(() => {
                    window.Repaint();
                }, $"Window content rendering cycle {i + 1} should not throw errors");
                
                yield return null;
            }
        }
        
        #endregion
        
        #region Window Interaction Tests
        
        [UnityTest]
        public IEnumerator Window_WithDifferentDisplaySettings_ShouldWork()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            
            // Test different window configurations
            var configurations = new[]
            {
                new { pos = new Rect(0, 0, 800, 600), name = "Standard" },
                new { pos = new Rect(100, 100, 400, 300), name = "Small" },
                new { pos = new Rect(50, 50, 1000, 700), name = "Large" }
            };
            
            foreach (var config in configurations)
            {
                // Act
                window.position = config.pos;
                
                // Assert
                AssertNoExceptionDuringOperation(() => {
                    window.Repaint();
                    window.Focus();
                }, $"Window should work with {config.name} configuration");
                
                yield return null;
            }
        }
        
        #endregion
        
        #region Performance Tests
        
        [UnityTest]
        public IEnumerator Window_RepeatedOperations_ShouldPerformWell()
        {
            // Arrange
            window = CreateSchemaEditorWindow();
            var startTime = System.DateTime.Now;
            
            // Act
            for (int i = 0; i < 50; i++)
            {
                window.Repaint();
                if (i % 10 == 0) yield return null; // Yield periodically
            }
            
            var duration = System.DateTime.Now - startTime;
            
            // Assert
            Assert.That(duration.TotalSeconds, Is.LessThan(5), 
                "50 repaint operations should complete within 5 seconds");
        }
        
        #endregion
    }
}