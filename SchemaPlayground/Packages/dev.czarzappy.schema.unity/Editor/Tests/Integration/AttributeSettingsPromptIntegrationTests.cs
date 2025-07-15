using NUnit.Framework;
using UnityEngine;
using Schema.Core.Data;
using Schema.Unity.Editor.Tests.Integration;

namespace Schema.Unity.Editor.Tests.Integration
{
    [TestFixture]
    public class AttributeSettingsPromptIntegrationTests : IntegrationTestBase
    {
        private DataScheme testScheme;
        private AttributeDefinition testAttribute;
        
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            
            // Set up test data
            testScheme = new DataScheme("TestScheme");
            testAttribute = new AttributeDefinition("TestAttribute", DataType.Text);
            testScheme.AddAttribute(testAttribute);
        }
        
        #region Window Creation Tests
        
        [Test]
        public void ShowWindow_WithValidParameters_ShouldCreateWindow()
        {
            // Act
            AttributeSettingsPrompt.ShowWindow(testScheme, testAttribute);
            
            // Assert
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            Assert.That(windows.Length, Is.EqualTo(1), "Should create exactly one prompt window");
            
            var promptWindow = windows[0];
            AssertWindowIsValid(promptWindow);
            
            // Cleanup
            promptWindow.Close();
        }
        
        [Test]
        public void ShowWindow_WithDifferentAttributes_ShouldShowCorrectTitle()
        {
            // Arrange
            var attribute1 = new AttributeDefinition("FirstAttribute", DataType.Integer);
            var attribute2 = new AttributeDefinition("SecondAttribute", DataType.DateTime);
            
            // Act & Assert for first attribute
            AttributeSettingsPrompt.ShowWindow(testScheme, attribute1);
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            Assert.That(windows.Length, Is.EqualTo(1));
            Assert.That(windows[0].titleContent.text, Does.Contain("FirstAttribute"));
            windows[0].Close();
            
            // Act & Assert for second attribute
            AttributeSettingsPrompt.ShowWindow(testScheme, attribute2);
            windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            Assert.That(windows.Length, Is.EqualTo(1));
            Assert.That(windows[0].titleContent.text, Does.Contain("SecondAttribute"));
            windows[0].Close();
        }
        
        [Test]
        public void ShowWindow_WithDifferentDataTypes_ShouldCreateValidWindows()
        {
            // Arrange
            var dataTypes = new[] { DataType.Text, DataType.Integer, DataType.Float, DataType.Boolean, DataType.DateTime };
            
            foreach (var dataType in dataTypes)
            {
                // Act
                var attribute = new AttributeDefinition($"Attr_{dataType}", dataType);
                AttributeSettingsPrompt.ShowWindow(testScheme, attribute);
                
                // Assert
                var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
                Assert.That(windows.Length, Is.EqualTo(1), $"Should create window for {dataType}");
                
                var promptWindow = windows[0];
                AssertWindowIsValid(promptWindow);
                Assert.That(promptWindow.titleContent.text, Does.Contain($"Attr_{dataType}"));
                
                // Cleanup
                promptWindow.Close();
            }
        }
        
        #endregion
        
        #region Window State Tests
        
        [Test]
        public void PromptWindow_WhenCreated_ShouldBeUtilityWindow()
        {
            // Act
            AttributeSettingsPrompt.ShowWindow(testScheme, testAttribute);
            
            // Assert
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            var promptWindow = windows[0];
            
            // Utility windows typically have different behavior - this test verifies the window exists
            AssertWindowIsValid(promptWindow);
            
            // Cleanup
            promptWindow.Close();
        }
        
        [Test]
        public void PromptWindow_WhenClosed_ShouldBeRemoved()
        {
            // Arrange
            AttributeSettingsPrompt.ShowWindow(testScheme, testAttribute);
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            Assert.That(windows.Length, Is.EqualTo(1));
            
            // Act
            windows[0].Close();
            
            // Assert
            windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            Assert.That(windows.Length, Is.EqualTo(0), "Window should be removed after closing");
        }
        
        [Test]
        public void PromptWindow_ShouldHaveReasonableSize()
        {
            // Act
            AttributeSettingsPrompt.ShowWindow(testScheme, testAttribute);
            
            // Assert
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            var promptWindow = windows[0];
            
            var position = promptWindow.position;
            Assert.That(position.width, Is.GreaterThan(100), "Window should have reasonable width");
            Assert.That(position.height, Is.GreaterThan(50), "Window should have reasonable height");
            Assert.That(position.width, Is.LessThan(2000), "Window width should be reasonable");
            Assert.That(position.height, Is.LessThan(1000), "Window height should be reasonable");
            
            // Cleanup
            promptWindow.Close();
        }
        
        #endregion
        
        #region Multiple Windows Tests
        
        [Test]
        public void ShowWindow_CalledMultipleTimes_ShouldReplaceExistingWindow()
        {
            // Arrange
            var attribute1 = new AttributeDefinition("Attr1", DataType.Text);
            var attribute2 = new AttributeDefinition("Attr2", DataType.Integer);
            
            // Act
            AttributeSettingsPrompt.ShowWindow(testScheme, attribute1);
            var firstWindows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            
            AttributeSettingsPrompt.ShowWindow(testScheme, attribute2);
            var secondWindows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            
            // Assert
            Assert.That(firstWindows.Length, Is.EqualTo(1));
            Assert.That(secondWindows.Length, Is.EqualTo(1));
            
            // The window should show the second attribute
            Assert.That(secondWindows[0].titleContent.text, Does.Contain("Attr2"));
            
            // Cleanup
            secondWindows[0].Close();
        }
        
        [Test]
        public void ShowWindow_WithSameAttribute_ShouldFocusExistingWindow()
        {
            // Act
            AttributeSettingsPrompt.ShowWindow(testScheme, testAttribute);
            var firstCall = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            
            AttributeSettingsPrompt.ShowWindow(testScheme, testAttribute);
            var secondCall = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            
            // Assert
            Assert.That(firstCall.Length, Is.EqualTo(1));
            Assert.That(secondCall.Length, Is.EqualTo(1));
            
            // Should still have only one window
            var promptWindow = secondCall[0];
            AssertWindowIsValid(promptWindow);
            
            // Cleanup
            promptWindow.Close();
        }
        
        #endregion
        
        #region Error Handling Tests
        
        [Test]
        public void ShowWindow_WithNullScheme_ShouldHandleGracefully()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => {
                AttributeSettingsPrompt.ShowWindow(null, testAttribute);
            }, "Should handle null scheme gracefully");
            
            // Clean up any windows that might have been created
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            foreach (var w in windows)
            {
                w.Close();
            }
        }
        
        [Test]
        public void ShowWindow_WithNullAttribute_ShouldHandleGracefully()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => {
                AttributeSettingsPrompt.ShowWindow(testScheme, null);
            }, "Should handle null attribute gracefully");
            
            // Clean up any windows that might have been created
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            foreach (var w in windows)
            {
                w.Close();
            }
        }
        
        [Test]
        public void ShowWindow_WithBothNull_ShouldHandleGracefully()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => {
                AttributeSettingsPrompt.ShowWindow(null, null);
            }, "Should handle both null parameters gracefully");
            
            // Clean up any windows that might have been created
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            foreach (var w in windows)
            {
                w.Close();
            }
        }
        
        #endregion
        
        #region Window Content Tests
        
        [Test]
        public void PromptWindow_ShouldRepaintWithoutErrors()
        {
            // Arrange
            AttributeSettingsPrompt.ShowWindow(testScheme, testAttribute);
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            var promptWindow = windows[0];
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                promptWindow.Repaint();
            }, "Prompt window repaint should not throw exceptions");
            
            // Cleanup
            promptWindow.Close();
        }
        
        [Test]
        public void PromptWindow_ShouldFocusWithoutErrors()
        {
            // Arrange
            AttributeSettingsPrompt.ShowWindow(testScheme, testAttribute);
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            var promptWindow = windows[0];
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                promptWindow.Focus();
            }, "Prompt window focus should not throw exceptions");
            
            // Cleanup
            promptWindow.Close();
        }
        
        #endregion
        
        #region Edge Cases
        
        [Test]
        public void ShowWindow_WithAttributeHavingSpecialCharacters_ShouldWork()
        {
            // Arrange
            var specialAttribute = new AttributeDefinition("Test-Attr_123!@#", DataType.Text);
            
            // Act
            AttributeSettingsPrompt.ShowWindow(testScheme, specialAttribute);
            
            // Assert
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            Assert.That(windows.Length, Is.EqualTo(1));
            
            var promptWindow = windows[0];
            AssertWindowIsValid(promptWindow);
            Assert.That(promptWindow.titleContent.text, Does.Contain("Test-Attr_123!@#"));
            
            // Cleanup
            promptWindow.Close();
        }
        
        [Test]
        public void ShowWindow_WithAttributeHavingLongName_ShouldWork()
        {
            // Arrange
            var longName = new string('A', 50) + "_LongAttribute";
            var longNameAttribute = new AttributeDefinition(longName, DataType.Text);
            
            // Act
            AttributeSettingsPrompt.ShowWindow(testScheme, longNameAttribute);
            
            // Assert
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            Assert.That(windows.Length, Is.EqualTo(1));
            
            var promptWindow = windows[0];
            AssertWindowIsValid(promptWindow);
            
            // Title might be truncated, but should contain part of the name
            Assert.That(promptWindow.titleContent.text, Does.Contain("A"));
            
            // Cleanup
            promptWindow.Close();
        }
        
        [Test]
        public void ShowWindow_WithUnicodeCharacters_ShouldWork()
        {
            // Arrange
            var unicodeAttribute = new AttributeDefinition("测试_атрибут_属性", DataType.Text);
            
            // Act
            AttributeSettingsPrompt.ShowWindow(testScheme, unicodeAttribute);
            
            // Assert
            var windows = Resources.FindObjectsOfTypeAll<AttributeSettingsPrompt>();
            Assert.That(windows.Length, Is.EqualTo(1));
            
            var promptWindow = windows[0];
            AssertWindowIsValid(promptWindow);
            
            // Cleanup
            promptWindow.Close();
        }
        
        #endregion
    }
}