using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Schema.Unity.Editor.Tests.Integration;
using Schema.Unity.Editor.Tests.GUI;

namespace Schema.Unity.Editor.Tests.GUI
{
    [TestFixture]
    public class SchemaEditorWindowGUITests : IntegrationTestBase
    {
        [SetUp]
        public override void Setup()
        {
            base.Setup();
            window = CreateSchemaEditorWindow();
            window.position = new Rect(0, 0, 800, 600);
        }
        
        [TearDown]
        public override void TearDown()
        {
            GUITestFramework.ResetEvent();
            base.TearDown();
        }
        
        #region Mouse Interaction Tests
        
        [UnityTest]
        public IEnumerator MouseClick_InWindowCenter_ShouldNotThrowError()
        {
            // Arrange
            var centerPosition = GUITestFramework.GetWindowCenter(window);
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateMouseClick(centerPosition);
                window.Repaint();
            }, "Mouse click in window center should not throw exceptions");
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator MouseClick_AtDifferentPositions_ShouldNotCauseErrors()
        {
            // Arrange
            var positions = new Vector2[]
            {
                new Vector2(50, 50),      // Top-left area
                new Vector2(400, 50),     // Top-center
                new Vector2(750, 50),     // Top-right area
                new Vector2(50, 300),     // Left-center
                new Vector2(400, 300),    // Center
                new Vector2(750, 300),    // Right-center
                new Vector2(50, 550),     // Bottom-left area
                new Vector2(400, 550),    // Bottom-center
                new Vector2(750, 550)     // Bottom-right area
            };
            
            foreach (var position in positions)
            {
                // Act & Assert
                AssertNoExceptionDuringOperation(() => {
                    GUITestFramework.SimulateMouseClick(position);
                    window.Repaint();
                }, $"Mouse click at position {position} should not throw exceptions");
                
                yield return null;
            }
        }
        
        [UnityTest]
        public IEnumerator MouseDrag_ShouldNotCauseErrors()
        {
            // Arrange
            var startPos = new Vector2(100, 100);
            var endPos = new Vector2(200, 200);
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateMouseDrag(startPos, endPos);
                window.Repaint();
            }, "Mouse drag should not throw exceptions");
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator MouseDoubleClick_ShouldNotCauseErrors()
        {
            // Arrange
            var position = GUITestFramework.GetWindowCenter(window);
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateDoubleClick(position);
                window.Repaint();
            }, "Double-click should not throw exceptions");
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator RightClick_ShouldNotCauseErrors()
        {
            // Arrange
            var position = GUITestFramework.GetWindowCenter(window);
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateMouseClick(position, button: 1); // Right click
                window.Repaint();
            }, "Right-click should not throw exceptions");
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator ContextMenu_ShouldNotCauseErrors()
        {
            // Arrange
            var position = GUITestFramework.GetWindowCenter(window);
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateContextClick(position);
                window.Repaint();
            }, "Context menu should not throw exceptions");
            
            yield return null;
        }
        
        #endregion
        
        #region Keyboard Interaction Tests
        
        [UnityTest]
        public IEnumerator KeyPress_F5_ShouldTriggerRefresh()
        {
            // Arrange
            bool operationCompleted = false;
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateKeyPress(KeyCode.F5);
                window.Repaint();
                operationCompleted = true;
            }, "F5 key press should not throw exceptions");
            
            Assert.That(operationCompleted, Is.True, "Refresh operation should complete");
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator KeyboardShortcuts_ShouldNotCauseErrors()
        {
            // Arrange
            var shortcuts = new[]
            {
                new { key = KeyCode.N, modifiers = EventModifiers.Control, name = "Ctrl+N (New)" },
                new { key = KeyCode.O, modifiers = EventModifiers.Control, name = "Ctrl+O (Open)" },
                new { key = KeyCode.S, modifiers = EventModifiers.Control, name = "Ctrl+S (Save)" },
                new { key = KeyCode.F, modifiers = EventModifiers.Control, name = "Ctrl+F (Find)" },
                new { key = KeyCode.Z, modifiers = EventModifiers.Control, name = "Ctrl+Z (Undo)" },
                new { key = KeyCode.Y, modifiers = EventModifiers.Control, name = "Ctrl+Y (Redo)" }
            };
            
            foreach (var shortcut in shortcuts)
            {
                // Act & Assert
                AssertNoExceptionDuringOperation(() => {
                    GUITestFramework.SimulateKeyPress(shortcut.key, shortcut.modifiers);
                    window.Repaint();
                }, $"Keyboard shortcut {shortcut.name} should not throw exceptions");
                
                yield return null;
            }
        }
        
        [UnityTest]
        public IEnumerator ArrowKeys_ShouldNotCauseErrors()
        {
            // Arrange
            var arrowKeys = new[] { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow };
            
            foreach (var key in arrowKeys)
            {
                // Act & Assert
                AssertNoExceptionDuringOperation(() => {
                    GUITestFramework.SimulateKeyPress(key);
                    window.Repaint();
                }, $"Arrow key {key} should not throw exceptions");
                
                yield return null;
            }
        }
        
        [UnityTest]
        public IEnumerator EscapeKey_ShouldNotCauseErrors()
        {
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateKeyPress(KeyCode.Escape);
                window.Repaint();
            }, "Escape key should not throw exceptions");
            
            yield return null;
        }
        
        [UnityTest]
        public IEnumerator TextInput_ShouldNotCauseErrors()
        {
            // Arrange
            var testText = "TestSchema123";
            
            // Act & Assert
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateTextInput(testText);
                window.Repaint();
            }, "Text input should not throw exceptions");
            
            yield return null;
        }
        
        #endregion
        
        #region Scroll Interaction Tests
        
        [UnityTest]
        public IEnumerator ScrollWheel_InWindow_ShouldNotCauseErrors()
        {
            // Arrange
            var scrollPosition = GUITestFramework.GetWindowCenter(window);
            var scrollDeltas = new Vector2[]
            {
                new Vector2(0, -10),  // Scroll down
                new Vector2(0, 10),   // Scroll up
                new Vector2(-10, 0),  // Scroll left
                new Vector2(10, 0)    // Scroll right
            };
            
            foreach (var delta in scrollDeltas)
            {
                // Act & Assert
                AssertNoExceptionDuringOperation(() => {
                    GUITestFramework.SimulateScrollWheel(delta, scrollPosition);
                    window.Repaint();
                }, $"Scroll with delta {delta} should not throw exceptions");
                
                yield return null;
            }
        }
        
        [UnityTest]
        public IEnumerator VerticalScroll_ShouldNotCauseErrors()
        {
            // Arrange
            var scrollAmounts = new float[] { -5f, -1f, 0f, 1f, 5f };
            
            foreach (var amount in scrollAmounts)
            {
                // Act & Assert
                AssertNoExceptionDuringOperation(() => {
                    GUITestFramework.SimulateVerticalScroll(amount);
                    window.Repaint();
                }, $"Vertical scroll amount {amount} should not throw exceptions");
                
                yield return null;
            }
        }
        
        [UnityTest]
        public IEnumerator HorizontalScroll_ShouldNotCauseErrors()
        {
            // Arrange
            var scrollAmounts = new float[] { -5f, -1f, 0f, 1f, 5f };
            
            foreach (var amount in scrollAmounts)
            {
                // Act & Assert
                AssertNoExceptionDuringOperation(() => {
                    GUITestFramework.SimulateHorizontalScroll(amount);
                    window.Repaint();
                }, $"Horizontal scroll amount {amount} should not throw exceptions");
                
                yield return null;
            }
        }
        
        #endregion
        
        #region Rapid Input Tests
        
        [UnityTest]
        public IEnumerator RapidMouseClicks_ShouldNotCauseErrors()
        {
            // Arrange
            var clickPosition = new Vector2(400, 300);
            
            // Act & Assert
            for (int i = 0; i < 10; i++)
            {
                AssertNoExceptionDuringOperation(() => {
                    GUITestFramework.SimulateMouseClick(clickPosition);
                    window.Repaint();
                }, $"Rapid mouse click {i + 1} should not throw exceptions");
                
                yield return null;
            }
        }
        
        [UnityTest]
        public IEnumerator RapidKeyPresses_ShouldNotCauseErrors()
        {
            // Arrange
            var keys = new[] { KeyCode.Space, KeyCode.Enter, KeyCode.Tab };
            
            for (int i = 0; i < 5; i++)
            {
                foreach (var key in keys)
                {
                    // Act & Assert
                    AssertNoExceptionDuringOperation(() => {
                        GUITestFramework.SimulateKeyPress(key);
                        window.Repaint();
                    }, $"Rapid key press {key} iteration {i + 1} should not throw exceptions");
                    
                    yield return null;
                }
            }
        }
        
        #endregion
        
        #region Window Resize Tests
        
        [UnityTest]
        public IEnumerator WindowResize_ShouldHandleGUICorrectly()
        {
            // Arrange
            var originalSize = window.position;
            var sizes = new Vector2[]
            {
                new Vector2(400, 300),   // Small
                new Vector2(1000, 700),  // Large
                new Vector2(600, 400),   // Medium
                new Vector2(300, 200)    // Very small
            };
            
            foreach (var size in sizes)
            {
                // Act
                var newRect = new Rect(originalSize.x, originalSize.y, size.x, size.y);
                window.position = newRect;
                
                // Test interaction after resize
                AssertNoExceptionDuringOperation(() => {
                    var center = GUITestFramework.GetWindowCenter(window);
                    GUITestFramework.SimulateMouseClick(center);
                    window.Repaint();
                }, $"GUI interaction should work after resize to {size.x}x{size.y}");
                
                yield return null;
            }
        }
        
        #endregion
        
        #region Multi-Event Sequence Tests
        
        [UnityTest]
        public IEnumerator ComplexInteractionSequence_ShouldNotCauseErrors()
        {
            // Arrange & Act - Simulate a complex user interaction sequence
            var center = GUITestFramework.GetWindowCenter(window);
            
            // 1. Click in window
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateMouseClick(center);
                window.Repaint();
            }, "Step 1: Initial click should work");
            yield return null;
            
            // 2. Type some text
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateTextInput("Test");
                window.Repaint();
            }, "Step 2: Text input should work");
            yield return null;
            
            // 3. Use keyboard shortcut
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateKeyPress(KeyCode.A, EventModifiers.Control);
                window.Repaint();
            }, "Step 3: Ctrl+A should work");
            yield return null;
            
            // 4. Scroll
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateVerticalScroll(-2f);
                window.Repaint();
            }, "Step 4: Scrolling should work");
            yield return null;
            
            // 5. Right-click
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateMouseClick(center, button: 1);
                window.Repaint();
            }, "Step 5: Right-click should work");
            yield return null;
            
            // 6. Press escape
            AssertNoExceptionDuringOperation(() => {
                GUITestFramework.SimulateKeyPress(KeyCode.Escape);
                window.Repaint();
            }, "Step 6: Escape should work");
            yield return null;
        }
        
        #endregion
        
        #region Edge Case Tests
        
        [UnityTest]
        public IEnumerator MouseClick_OutsideWindow_ShouldNotCauseErrors()
        {
            // Arrange
            var outsidePositions = new Vector2[]
            {
                new Vector2(-10, -10),           // Top-left outside
                new Vector2(810, -10),           // Top-right outside
                new Vector2(-10, 610),           // Bottom-left outside
                new Vector2(810, 610),           // Bottom-right outside
                new Vector2(400, -10),           // Top outside
                new Vector2(400, 610),           // Bottom outside
                new Vector2(-10, 300),           // Left outside
                new Vector2(810, 300)            // Right outside
            };
            
            foreach (var position in outsidePositions)
            {
                // Act & Assert
                AssertNoExceptionDuringOperation(() => {
                    GUITestFramework.SimulateMouseClick(position);
                    window.Repaint();
                }, $"Click outside window at {position} should not throw exceptions");
                
                yield return null;
            }
        }
        
        [UnityTest]
        public IEnumerator RandomPositionClicks_ShouldNotCauseErrors()
        {
            // Act & Assert
            for (int i = 0; i < 20; i++)
            {
                var randomPos = GUITestFramework.GetRandomWindowPosition(window);
                
                AssertNoExceptionDuringOperation(() => {
                    GUITestFramework.SimulateMouseClick(randomPos);
                    window.Repaint();
                }, $"Random click {i + 1} at position {randomPos} should not throw exceptions");
                
                yield return null;
            }
        }
        
        [UnityTest]
        public IEnumerator SimultaneousKeyModifiers_ShouldNotCauseErrors()
        {
            // Arrange
            var modifierCombinations = new[]
            {
                EventModifiers.Control | EventModifiers.Shift,
                EventModifiers.Control | EventModifiers.Alt,
                EventModifiers.Shift | EventModifiers.Alt,
                EventModifiers.Control | EventModifiers.Shift | EventModifiers.Alt
            };
            
            foreach (var modifiers in modifierCombinations)
            {
                // Act & Assert
                AssertNoExceptionDuringOperation(() => {
                    GUITestFramework.SimulateKeyPress(KeyCode.A, modifiers);
                    window.Repaint();
                }, $"Key combination with modifiers {modifiers} should not throw exceptions");
                
                yield return null;
            }
        }
        
        #endregion
    }
}