using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback.Tests
{
    /// <summary>A window that paints a known marker colour in each corner of its content area.</summary>
    public sealed class CaptureMarkerWindow : EditorWindow
    {
        internal const float MarkerPoints = 40f;

        private void OnGUI()
        {
            var area = new Rect(0, 0, position.width, position.height);
            EditorGUI.DrawRect(area, new Color(0.5f, 0.5f, 0.5f));
            EditorGUI.DrawRect(new Rect(0, 0, MarkerPoints, MarkerPoints), Color.red);
            EditorGUI.DrawRect(new Rect(area.width - MarkerPoints, 0, MarkerPoints, MarkerPoints), Color.green);
            EditorGUI.DrawRect(new Rect(0, area.height - MarkerPoints, MarkerPoints, MarkerPoints), Color.blue);
            EditorGUI.DrawRect(new Rect(area.width - MarkerPoints, area.height - MarkerPoints, MarkerPoints, MarkerPoints), Color.yellow);
            GUI.Label(new Rect(MarkerPoints + 20, MarkerPoints + 20, 420, 20), "Capture marker: red top-left, green top-right, blue bottom-left, yellow bottom-right");
        }
    }

    /// <summary>
    /// Manual verification of the window capture on a real window, kept because it is the only check of the framing.
    /// It cannot be a batch test (it needs a GPU, a window server and a repaint), so it is a menu item and an
    /// <c>-executeMethod</c> entry point for a GUI Editor (not <c>-batchmode</c>):
    /// <code>
    /// SCHEMA_VERIFY_OUT=/some/dir Unity -projectPath TestProjects/Unity6 \
    ///   -executeMethod Schema.Unity.Editor.Feedback.Tests.WindowCaptureVerify.Run -logFile /some/dir/verify.log
    /// </code>
    /// It opens <see cref="CaptureMarkerWindow"/> and the Schema Editor, waits 240 update ticks for each to draw,
    /// captures each with <see cref="WindowCapture"/>, then presses the toolbar button's path (<c>FeedbackFlow.Begin</c>)
    /// and captures the dialog that opens. It writes <c>marker.png</c>, <c>schema_editor.png</c>, <c>dialog.png</c> and
    /// <c>verify.txt</c> into the output folder, and exits 0 only if the marker picture is framed correctly: the right
    /// size (content points x pixelsPerPoint), the four marker colours in the four corners (so upright, not mirrored,
    /// and no tab strip), not black. The Schema Editor picture is for looking at.
    /// </summary>
    public static class WindowCaptureVerify
    {
        private const int WaitTicks = 240;

        [MenuItem("Tools/Schema/Feedback/Verify Window Capture")]
        public static void Run()
        {
            var outDir = Environment.GetEnvironmentVariable("SCHEMA_VERIFY_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "schema-capture-verify");
            Directory.CreateDirectory(outDir);

            var report = new StringBuilder();
            var failures = 0;
            void Say(string line) { report.AppendLine(line); Debug.Log("[capture-verify] " + line); }

            var marker = ScriptableObject.CreateInstance<CaptureMarkerWindow>();
            marker.titleContent = new GUIContent("Capture marker");
            marker.position = new Rect(200, 200, 640, 400);
            marker.Show();
            marker.Focus();

            WaitTicksThen(marker, () =>
            {
                failures += VerifyMarker(marker, outDir, Say);
                marker.Close();

                // The Schema Editor as its own floating window.
                var schema = EditorWindow.GetWindow<SchemaEditorWindow>("Schema Editor");
                schema.position = new Rect(200, 200, 1000, 700);
                schema.Show();
                schema.Focus();
                WaitTicksThen(schema, () =>
                {
                    CaptureTo(schema, "schema_editor.png", outDir, Say);
                    schema.Close();

                    // The Schema Editor docked as a tab beside the Scene view, the way most people keep it.
                    var docked = EditorWindow.GetWindow<SchemaEditorWindow>("Schema Editor", true, typeof(SceneView));
                    docked.Focus();
                    WaitTicksThen(docked, () =>
                    {
                        failures += CaptureTo(docked, "schema_editor_docked.png", outDir, Say) ? 0 : 1;

                        // The toolbar button's path: capture the Schema Editor, then open the dialog (with that picture in it).
                        FeedbackWindow.UseUtilityWindow = false; // see the note on that field
                        FeedbackFlow.Begin(docked);
                        WaitTicksThen(docked, () =>
                        {
                            var dialogs = Resources.FindObjectsOfTypeAll<FeedbackWindow>();
                            if (dialogs.Length == 0)
                            {
                                Say("dialog: did not open");
                                failures++;
                            }
                            else
                            {
                                Say(Describe(dialogs[0]));
                                CaptureTo(dialogs[0], "dialog.png", outDir, Say);
                                dialogs[0].Close();
                            }

                            Say(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + failures + ")");
                            File.WriteAllText(Path.Combine(outDir, "verify.txt"), report.ToString());
                            if (Application.isBatchMode || Environment.GetEnvironmentVariable("SCHEMA_VERIFY_EXIT") == "1")
                            {
                                EditorApplication.Exit(failures == 0 ? 0 : 1);
                            }
                        },
                        // a window only repaints when asked; with no input the dialog would otherwise be drawn once, half-built
                        each: () => { foreach (var open in Resources.FindObjectsOfTypeAll<FeedbackWindow>()) open.Repaint(); });
                    });
                });
            });
        }

        /// <summary>
        /// Batch-safe smoke check of the socket client against a real daemon: sends <c>ping</c> only (never <c>submit</c>) to the
        /// socket in <c>SCHEMA_VERIFY_SOCKET</c> and exits 0 if it answers. Run with
        /// <c>-batchmode -nographics -quit -executeMethod Schema.Unity.Editor.Feedback.Tests.WindowCaptureVerify.PingDaemon</c>.
        /// </summary>
        public static void PingDaemon()
        {
            var socket = Environment.GetEnvironmentVariable("SCHEMA_VERIFY_SOCKET");
            try
            {
                var data = new RepairkitClient(socket, 5000).Ping();
                Debug.Log("[capture-verify] ping ok: " + data);
                EditorApplication.Exit(0);
            }
            catch (RepairkitException e)
            {
                Debug.LogError("[capture-verify] ping failed: " + e.Message);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// End-to-end check of the collector against a real Schema-Unity daemon (plan A1.1, phase 2): <c>ping</c>, then one
        /// report through the collector's own <see cref="FeedbackSender"/> with the live Editor's context and a small
        /// generated PNG named like the real screenshot. The socket comes from <c>SCHEMA_VERIFY_SOCKET</c> and the outbox
        /// is a folder under <c>SCHEMA_VERIFY_OUT</c>, so neither the shared EditorPrefs key nor the shared outbox is
        /// touched. Writes <c>e2e.txt</c> (status and report id) there and exits 0 only if the report and its photo were
        /// both accepted. Batch-safe:
        /// <code>
        /// SCHEMA_VERIFY_SOCKET=~/.schema-unity/repairkit/repairkit.sock SCHEMA_VERIFY_OUT=/some/dir \
        ///   unity run TestProjects/Unity6 -- -executeMethod Schema.Unity.Editor.Feedback.Tests.WindowCaptureVerify.SubmitToDaemon
        /// </code>
        /// It files a real report (message starts "[phase 2 e2e smoke]"): point it only at a daemon with repairs held.
        /// </summary>
        public static void SubmitToDaemon()
        {
            var socket = Environment.GetEnvironmentVariable("SCHEMA_VERIFY_SOCKET");
            var outDir = Environment.GetEnvironmentVariable("SCHEMA_VERIFY_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "schema-daemon-e2e");
            Directory.CreateDirectory(outDir);
            var lines = new StringBuilder();
            void Say(string line) { lines.AppendLine(line); Debug.Log("[daemon-e2e] " + line); }

            var exitCode = 1;
            try
            {
                Say("ping: " + new RepairkitClient(socket, 5000).Ping().ToString(Newtonsoft.Json.Formatting.None));

                var texture = new Texture2D(64, 40, TextureFormat.RGBA32, false);
                var pixels = new Color32[64 * 40];
                for (var i = 0; i < pixels.Length; i++) pixels[i] = (i % 64) < 32 ? new Color32(200, 40, 40, 255) : new Color32(40, 40, 200, 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                var photo = PhotoFitter.Fit(texture);
                UnityEngine.Object.DestroyImmediate(texture);

                var draft = new FeedbackDraft
                {
                    Message = "[phase 2 e2e smoke] Not a real bug: a test report from WindowCaptureVerify.SubmitToDaemon proving the " +
                              "collector reaches the Schema-Unity daemon. Do not repair.",
                    Context = FeedbackContext.Build(FeedbackContextCapture.Capture(null, false)),
                    Photo = photo,
                };
                var settings = new FeedbackSettings { SocketPath = socket, OutboxRoot = Path.Combine(outDir, "outbox") };
                var result = new FeedbackSender(settings).Send(draft);
                Say("send: " + result.Status + ", report " + result.ReportId + ", photo " + photo.Name + " (" + photo.Bytes.Length +
                    " bytes), error " + (result.Error ?? "none") + ": " + result.Message);
                Say("report_id=" + result.ReportId);
                exitCode = result.Status == SendStatus.Sent && !string.IsNullOrEmpty(result.ReportId) && result.Error == null ? 0 : 1;
            }
            catch (Exception e)
            {
                Say("failed: " + e.GetType().Name + ": " + e.Message);
            }

            Say(exitCode == 0 ? "RESULT: PASS" : "RESULT: FAIL");
            File.WriteAllText(Path.Combine(outDir, "e2e.txt"), lines.ToString());
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Captures one window to a PNG and says how big it came out against the window's size times the scale.</summary>
        private static bool CaptureTo(EditorWindow window, string fileName, string outDir, Action<string> say)
        {
            var capture = WindowCapture.Capture(window);
            if (!capture.Ok)
            {
                say(fileName + ": no picture: " + capture.Problem);
                return false;
            }

            try
            {
                File.WriteAllBytes(Path.Combine(outDir, fileName), ImageConversion.EncodeToPNG(capture.Texture));
                var scale = EditorGUIUtility.pixelsPerPoint;
                var wantWidth = Mathf.RoundToInt(window.position.width * scale);
                var wantHeight = Mathf.RoundToInt(window.position.height * scale);
                var sized = Mathf.Abs(capture.Texture.width - wantWidth) <= 1 && Mathf.Abs(capture.Texture.height - wantHeight) <= 1;
                say(fileName + ": " + capture.Texture.width + " x " + capture.Texture.height + ", window " + window.position.size +
                    " pt x ppp " + scale + (sized ? " ok" : " SIZE MISMATCH"));
                return sized;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(capture.Texture);
            }
        }

        /// <summary>The facts a capture rests on, for one window: sizes, scale, and the view's own scale-like members.</summary>
        private static string Describe(EditorWindow window)
        {
            var text = new StringBuilder();
            text.Append("describe ").Append(window.GetType().Name).Append(": position ").Append(window.position)
                .Append(", ppp ").Append(EditorGUIUtility.pixelsPerPoint);
            try
            {
                var panel = window.rootVisualElement?.panel;
                if (panel != null)
                {
                    foreach (var name in new[] { "scaledPixelsPerPoint", "pixelsPerPoint", "visualTree" })
                    {
                        var property = panel.GetType().GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        if (property != null && name != "visualTree") text.Append(", panel.").Append(name).Append('=').Append(property.GetValue(panel));
                    }

                    text.Append(", panel ").Append(panel.GetType().Name);
                }
            }
            catch (Exception e) { text.Append(", panel read failed: ").Append(e.GetType().Name); }

            var view = typeof(EditorWindow).GetField("m_Parent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(window);
            if (view == null) return text.Append(", no view").ToString();

            text.Append(", view ").Append(view.GetType().Name);
            for (var type = view.GetType(); type != null; type = type.BaseType)
            {
                foreach (var property in type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    var name = property.Name.ToLowerInvariant();
                    if (!(name.Contains("pixel") || name.Contains("scale") || name.Contains("dpi") || name.Contains("zoom") || name == "position" || name == "screenposition")) continue;
                    try { text.Append(", ").Append(type.Name).Append('.').Append(property.Name).Append('=').Append(property.GetValue(view)); }
                    catch (Exception) { /* some getters need a context */ }
                }
            }

            return text.ToString();
        }

        private static void WaitTicksThen(EditorWindow window, Action then, Action each = null)
        {
            var ticks = 0;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (window != null) window.Repaint();
                each?.Invoke();
                if (++ticks < WaitTicks) return;
                EditorApplication.update -= tick;
                then();
            };
            EditorApplication.update += tick;
        }

        private static int VerifyMarker(EditorWindow marker, string outDir, Action<string> say)
        {
            say(Describe(marker));
            var capture = WindowCapture.Capture(marker);
            if (!capture.Ok)
            {
                say("marker: no picture: " + capture.Problem);
                return 1;
            }

            var texture = capture.Texture;
            try
            {
                File.WriteAllBytes(Path.Combine(outDir, "marker.png"), ImageConversion.EncodeToPNG(texture));
                var scale = EditorGUIUtility.pixelsPerPoint;
                var expectedWidth = Mathf.RoundToInt(marker.position.width * scale);
                var expectedHeight = Mathf.RoundToInt(marker.position.height * scale);
                say("marker: " + texture.width + " x " + texture.height + ", expected " + expectedWidth + " x " + expectedHeight +
                    " (window " + marker.position.size + " pt, ppp " + scale + ", " + SystemInfo.graphicsDeviceType + ")");

                var failures = 0;
                if (Mathf.Abs(texture.width - expectedWidth) > 1 || Mathf.Abs(texture.height - expectedHeight) > 1)
                {
                    say("FAIL size");
                    failures++;
                }

                const int inset = 10; // pixels in from each corner, well inside the 40 pt marker
                failures += CheckCorner(texture, "top-left", inset, texture.height - 1 - inset, Color.red, say);
                failures += CheckCorner(texture, "top-right", texture.width - 1 - inset, texture.height - 1 - inset, Color.green, say);
                failures += CheckCorner(texture, "bottom-left", inset, inset, Color.blue, say);
                failures += CheckCorner(texture, "bottom-right", texture.width - 1 - inset, inset, Color.yellow, say);
                return failures;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static int CheckCorner(Texture2D texture, string name, int x, int y, Color expected, Action<string> say)
        {
            var actual = texture.GetPixel(x, y);
            var close = Mathf.Abs(actual.r - expected.r) < 0.12f && Mathf.Abs(actual.g - expected.g) < 0.12f && Mathf.Abs(actual.b - expected.b) < 0.12f;
            say("corner " + name + ": " + (Color32)actual + " expected " + (Color32)expected + (close ? " ok" : " FAIL"));
            return close ? 0 : 1;
        }
    }
}
