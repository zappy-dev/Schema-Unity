using System;
using System.Collections;
using UnityEngine;
using NUnit.Framework;
using Schema.Core;
using UnityEditor;
using UnityEngine.TestTools;
using Logger = Schema.Core.Logging.Logger;

namespace Schema.Unity.Editor.Tests
{
    public class SchemaEditorWindowTests
    {
        SchemaEditorWindow window;

        [SetUp] public void Setup() => window = EditorWindowExt.Open<SchemaEditorWindow>();
        [TearDown] public void Teardown() => EditorWindowExt.Close(window);

        [UnityTest]
        public IEnumerator Test_Click_Create_New_Scheme_Button()
        {
            var guid = Guid.NewGuid();
            var newSchemeName = guid.ToString();
            Logger.Log($"Test_Click_Create_New_Scheme_Button: {newSchemeName}");
            // Initial Layout pass
            yield return EditorWindowExt.PumpFrames();

            yield return CreateNewSchemeFlow(newSchemeName);
        }

        private IEnumerator CreateNewSchemeFlow(string newSchemeName)
        {
            // Focus create new scheme field
            Logger.LogDbgVerbose($"Focus control: {SchemaEditorWindow.CONTROL_NAME_NEW_SCHEME_NAME_FIELD}");
            window.SetFocus(SchemaEditorWindow.CONTROL_NAME_NEW_SCHEME_NAME_FIELD);
            yield return EditorWindowExt.PumpFrames();

            // Simulate entering characters
            if (newSchemeName != null)
            {
                Logger.Log($"Test_Click_Create_New_Scheme_Button, type: {newSchemeName}");
                
                foreach (var ch in newSchemeName)
                {
                    var keyDown = Event.KeyboardEvent(ch.ToString());
                    keyDown.keyCode = KeyCode.None;
                    
                    window.Send(keyDown);
                    yield return EditorWindowExt.PumpFrames();

                    var keyUp = Event.KeyboardEvent(ch.ToString());
                    keyUp.type = EventType.KeyUp;
                    window.Send(keyUp);
                    yield return EditorWindowExt.PumpFrames();
                }

                window.releaseControl = true;
                yield return EditorWindowExt.PumpFrames();
            }
            
            // Click create new scheme button
            yield return window.SimClick_CreateNewSchema();
        
            yield return EditorWindowExt.PumpFrames(60);
            
            Logger.LogDbgVerbose($"Schema Editor Window events: {window.ReportEvents()}");

            var res = Schema.Core.Schema.GetScheme(newSchemeName, new SchemaContext
            {
                Driver = nameof(Test_Click_Create_New_Scheme_Button)
            });
            
            Assert.IsTrue(res.Passed, res.Message);
        }
    }
}
