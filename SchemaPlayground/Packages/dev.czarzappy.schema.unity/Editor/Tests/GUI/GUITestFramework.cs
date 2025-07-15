using UnityEngine;
using UnityEditor;
using System;

namespace Schema.Unity.Editor.Tests.GUI
{
    /// <summary>
    /// Custom framework for testing GUI interactions in Unity Editor windows
    /// Provides utilities for simulating user input events
    /// </summary>
    public static class GUITestFramework
    {
        private static Event currentTestEvent;
        
        #region Mouse Event Simulation
        
        /// <summary>
        /// Simulates a mouse event at the specified position
        /// </summary>
        /// <param name="eventType">Type of mouse event</param>
        /// <param name="mousePosition">Position of the mouse</param>
        /// <param name="button">Mouse button (0=left, 1=right, 2=middle)</param>
        /// <param name="modifiers">Keyboard modifiers</param>
        public static void SimulateMouseEvent(EventType eventType, Vector2 mousePosition, int button = 0, EventModifiers modifiers = EventModifiers.None)
        {
            currentTestEvent = new Event
            {
                type = eventType,
                mousePosition = mousePosition,
                button = button,
                modifiers = modifiers,
                delta = Vector2.zero,
                clickCount = 1
            };
            
            Event.current = currentTestEvent;
        }
        
        /// <summary>
        /// Simulates a mouse click (down + up) at the specified position
        /// </summary>
        /// <param name="position">Click position</param>
        /// <param name="button">Mouse button</param>
        /// <param name="modifiers">Keyboard modifiers</param>
        public static void SimulateMouseClick(Vector2 position, int button = 0, EventModifiers modifiers = EventModifiers.None)
        {
            // Simulate mouse down
            SimulateMouseEvent(EventType.MouseDown, position, button, modifiers);
            // Immediately simulate mouse up
            SimulateMouseEvent(EventType.MouseUp, position, button, modifiers);
        }
        
        /// <summary>
        /// Simulates a double-click at the specified position
        /// </summary>
        /// <param name="position">Click position</param>
        /// <param name="button">Mouse button</param>
        public static void SimulateDoubleClick(Vector2 position, int button = 0)
        {
            currentTestEvent = new Event
            {
                type = EventType.MouseDown,
                mousePosition = position,
                button = button,
                clickCount = 2,
                modifiers = EventModifiers.None
            };
            
            Event.current = currentTestEvent;
        }
        
        /// <summary>
        /// Simulates a mouse drag operation
        /// </summary>
        /// <param name="startPosition">Starting position</param>
        /// <param name="endPosition">Ending position</param>
        /// <param name="button">Mouse button</param>
        public static void SimulateMouseDrag(Vector2 startPosition, Vector2 endPosition, int button = 0)
        {
            // Mouse down at start
            SimulateMouseEvent(EventType.MouseDown, startPosition, button);
            
            // Mouse drag to end
            currentTestEvent = new Event
            {
                type = EventType.MouseDrag,
                mousePosition = endPosition,
                button = button,
                delta = endPosition - startPosition,
                modifiers = EventModifiers.None
            };
            Event.current = currentTestEvent;
            
            // Mouse up at end
            SimulateMouseEvent(EventType.MouseUp, endPosition, button);
        }
        
        /// <summary>
        /// Simulates mouse movement without any buttons pressed
        /// </summary>
        /// <param name="position">Mouse position</param>
        /// <param name="delta">Movement delta</param>
        public static void SimulateMouseMove(Vector2 position, Vector2 delta = default)
        {
            currentTestEvent = new Event
            {
                type = EventType.MouseMove,
                mousePosition = position,
                delta = delta,
                button = -1, // No button
                modifiers = EventModifiers.None
            };
            
            Event.current = currentTestEvent;
        }
        
        #endregion
        
        #region Keyboard Event Simulation
        
        /// <summary>
        /// Simulates a keyboard event
        /// </summary>
        /// <param name="eventType">Key event type</param>
        /// <param name="keyCode">Key code</param>
        /// <param name="modifiers">Modifier keys</param>
        /// <param name="character">Character representation</param>
        public static void SimulateKeyEvent(EventType eventType, KeyCode keyCode, EventModifiers modifiers = EventModifiers.None, char character = '\0')
        {
            if (character == '\0')
            {
                character = GetCharacterFromKeyCode(keyCode, modifiers);
            }
            
            currentTestEvent = new Event
            {
                type = eventType,
                keyCode = keyCode,
                modifiers = modifiers,
                character = character
            };
            
            Event.current = currentTestEvent;
        }
        
        /// <summary>
        /// Simulates a key press (down + up)
        /// </summary>
        /// <param name="keyCode">Key to press</param>
        /// <param name="modifiers">Modifier keys</param>
        public static void SimulateKeyPress(KeyCode keyCode, EventModifiers modifiers = EventModifiers.None)
        {
            SimulateKeyEvent(EventType.KeyDown, keyCode, modifiers);
            SimulateKeyEvent(EventType.KeyUp, keyCode, modifiers);
        }
        
        /// <summary>
        /// Simulates typing a string of characters
        /// </summary>
        /// <param name="text">Text to type</param>
        public static void SimulateTextInput(string text)
        {
            foreach (char c in text)
            {
                var keyCode = GetKeyCodeFromCharacter(c);
                var modifiers = char.IsUpper(c) ? EventModifiers.Shift : EventModifiers.None;
                
                SimulateKeyEvent(EventType.KeyDown, keyCode, modifiers, c);
                SimulateKeyEvent(EventType.KeyUp, keyCode, modifiers, c);
            }
        }
        
        #endregion
        
        #region Scroll Event Simulation
        
        /// <summary>
        /// Simulates mouse scroll wheel movement
        /// </summary>
        /// <param name="delta">Scroll delta (positive = up, negative = down)</param>
        /// <param name="position">Mouse position during scroll</param>
        public static void SimulateScrollWheel(Vector2 delta, Vector2 position = default)
        {
            if (position == default)
            {
                position = new Vector2(Screen.width / 2, Screen.height / 2);
            }
            
            currentTestEvent = new Event
            {
                type = EventType.ScrollWheel,
                delta = delta,
                mousePosition = position,
                modifiers = EventModifiers.None
            };
            
            Event.current = currentTestEvent;
        }
        
        /// <summary>
        /// Simulates vertical scrolling
        /// </summary>
        /// <param name="scrollAmount">Amount to scroll (positive = up, negative = down)</param>
        /// <param name="position">Mouse position</param>
        public static void SimulateVerticalScroll(float scrollAmount, Vector2 position = default)
        {
            SimulateScrollWheel(new Vector2(0, scrollAmount), position);
        }
        
        /// <summary>
        /// Simulates horizontal scrolling
        /// </summary>
        /// <param name="scrollAmount">Amount to scroll (positive = right, negative = left)</param>
        /// <param name="position">Mouse position</param>
        public static void SimulateHorizontalScroll(float scrollAmount, Vector2 position = default)
        {
            SimulateScrollWheel(new Vector2(scrollAmount, 0), position);
        }
        
        #endregion
        
        #region Context Menu Simulation
        
        /// <summary>
        /// Simulates a right-click context menu event
        /// </summary>
        /// <param name="position">Position for context menu</param>
        public static void SimulateContextClick(Vector2 position)
        {
            currentTestEvent = new Event
            {
                type = EventType.ContextClick,
                mousePosition = position,
                button = 1, // Right mouse button
                modifiers = EventModifiers.None
            };
            
            Event.current = currentTestEvent;
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Resets the current event to null
        /// </summary>
        public static void ResetEvent()
        {
            Event.current = null;
            currentTestEvent = null;
        }
        
        /// <summary>
        /// Gets the current test event
        /// </summary>
        /// <returns>Current test event</returns>
        public static Event GetCurrentEvent()
        {
            return currentTestEvent;
        }
        
        /// <summary>
        /// Gets the rectangle representing the entire window area
        /// </summary>
        /// <param name="window">Editor window</param>
        /// <returns>Window rectangle</returns>
        public static Rect GetWindowRect(EditorWindow window)
        {
            return new Rect(0, 0, window.position.width, window.position.height);
        }
        
        /// <summary>
        /// Gets the center point of a window
        /// </summary>
        /// <param name="window">Editor window</param>
        /// <returns>Center position</returns>
        public static Vector2 GetWindowCenter(EditorWindow window)
        {
            return new Vector2(window.position.width / 2, window.position.height / 2);
        }
        
        /// <summary>
        /// Gets a random point within the window bounds
        /// </summary>
        /// <param name="window">Editor window</param>
        /// <param name="margin">Margin from edges</param>
        /// <returns>Random position within window</returns>
        public static Vector2 GetRandomWindowPosition(EditorWindow window, float margin = 10f)
        {
            var rect = GetWindowRect(window);
            return new Vector2(
                UnityEngine.Random.Range(margin, rect.width - margin),
                UnityEngine.Random.Range(margin, rect.height - margin)
            );
        }
        
        /// <summary>
        /// Checks if a position is within window bounds
        /// </summary>
        /// <param name="window">Editor window</param>
        /// <param name="position">Position to check</param>
        /// <returns>True if position is within window</returns>
        public static bool IsPositionInWindow(EditorWindow window, Vector2 position)
        {
            var rect = GetWindowRect(window);
            return rect.Contains(position);
        }
        
        #endregion
        
        #region Private Helper Methods
        
        /// <summary>
        /// Maps KeyCode to character representation
        /// </summary>
        private static char GetCharacterFromKeyCode(KeyCode keyCode, EventModifiers modifiers)
        {
            bool isShift = (modifiers & EventModifiers.Shift) != 0;
            
            switch (keyCode)
            {
                case KeyCode.Space: return ' ';
                case KeyCode.Return: return '\n';
                case KeyCode.Tab: return '\t';
                case KeyCode.Backspace: return '\b';
                case KeyCode.Delete: return '\u007F';
                case KeyCode.Escape: return '\u001B';
                
                // Letters
                case KeyCode.A: return isShift ? 'A' : 'a';
                case KeyCode.B: return isShift ? 'B' : 'b';
                case KeyCode.C: return isShift ? 'C' : 'c';
                case KeyCode.D: return isShift ? 'D' : 'd';
                case KeyCode.E: return isShift ? 'E' : 'e';
                case KeyCode.F: return isShift ? 'F' : 'f';
                case KeyCode.G: return isShift ? 'G' : 'g';
                case KeyCode.H: return isShift ? 'H' : 'h';
                case KeyCode.I: return isShift ? 'I' : 'i';
                case KeyCode.J: return isShift ? 'J' : 'j';
                case KeyCode.K: return isShift ? 'K' : 'k';
                case KeyCode.L: return isShift ? 'L' : 'l';
                case KeyCode.M: return isShift ? 'M' : 'm';
                case KeyCode.N: return isShift ? 'N' : 'n';
                case KeyCode.O: return isShift ? 'O' : 'o';
                case KeyCode.P: return isShift ? 'P' : 'p';
                case KeyCode.Q: return isShift ? 'Q' : 'q';
                case KeyCode.R: return isShift ? 'R' : 'r';
                case KeyCode.S: return isShift ? 'S' : 's';
                case KeyCode.T: return isShift ? 'T' : 't';
                case KeyCode.U: return isShift ? 'U' : 'u';
                case KeyCode.V: return isShift ? 'V' : 'v';
                case KeyCode.W: return isShift ? 'W' : 'w';
                case KeyCode.X: return isShift ? 'X' : 'x';
                case KeyCode.Y: return isShift ? 'Y' : 'y';
                case KeyCode.Z: return isShift ? 'Z' : 'z';
                
                // Numbers
                case KeyCode.Alpha0: return isShift ? ')' : '0';
                case KeyCode.Alpha1: return isShift ? '!' : '1';
                case KeyCode.Alpha2: return isShift ? '@' : '2';
                case KeyCode.Alpha3: return isShift ? '#' : '3';
                case KeyCode.Alpha4: return isShift ? '$' : '4';
                case KeyCode.Alpha5: return isShift ? '%' : '5';
                case KeyCode.Alpha6: return isShift ? '^' : '6';
                case KeyCode.Alpha7: return isShift ? '&' : '7';
                case KeyCode.Alpha8: return isShift ? '*' : '8';
                case KeyCode.Alpha9: return isShift ? '(' : '9';
                
                default: return '\0';
            }
        }
        
        /// <summary>
        /// Maps character to KeyCode
        /// </summary>
        private static KeyCode GetKeyCodeFromCharacter(char character)
        {
            switch (char.ToLower(character))
            {
                case ' ': return KeyCode.Space;
                case '\n': return KeyCode.Return;
                case '\t': return KeyCode.Tab;
                case 'a': return KeyCode.A;
                case 'b': return KeyCode.B;
                case 'c': return KeyCode.C;
                case 'd': return KeyCode.D;
                case 'e': return KeyCode.E;
                case 'f': return KeyCode.F;
                case 'g': return KeyCode.G;
                case 'h': return KeyCode.H;
                case 'i': return KeyCode.I;
                case 'j': return KeyCode.J;
                case 'k': return KeyCode.K;
                case 'l': return KeyCode.L;
                case 'm': return KeyCode.M;
                case 'n': return KeyCode.N;
                case 'o': return KeyCode.O;
                case 'p': return KeyCode.P;
                case 'q': return KeyCode.Q;
                case 'r': return KeyCode.R;
                case 's': return KeyCode.S;
                case 't': return KeyCode.T;
                case 'u': return KeyCode.U;
                case 'v': return KeyCode.V;
                case 'w': return KeyCode.W;
                case 'x': return KeyCode.X;
                case 'y': return KeyCode.Y;
                case 'z': return KeyCode.Z;
                case '0': return KeyCode.Alpha0;
                case '1': return KeyCode.Alpha1;
                case '2': return KeyCode.Alpha2;
                case '3': return KeyCode.Alpha3;
                case '4': return KeyCode.Alpha4;
                case '5': return KeyCode.Alpha5;
                case '6': return KeyCode.Alpha6;
                case '7': return KeyCode.Alpha7;
                case '8': return KeyCode.Alpha8;
                case '9': return KeyCode.Alpha9;
                default: return KeyCode.None;
            }
        }
        
        #endregion
    }
}