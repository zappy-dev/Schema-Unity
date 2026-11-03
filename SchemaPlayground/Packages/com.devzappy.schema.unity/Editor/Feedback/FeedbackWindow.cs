using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>
    /// The dialog: what is wrong (required), the screenshot taken before this window opened, whether to attach
    /// the open scheme's data, and Send (Cmd/Ctrl+Enter). The context and the screenshot are captured when the
    /// dialog opens and kept in serialised fields, so they survive a domain reload and always describe the
    /// moment the person pressed the button, not the moment they pressed Send.
    /// </summary>
    internal sealed class FeedbackWindow : EditorWindow
    {
        [SerializeField] private string message = string.Empty;
        [SerializeField] private byte[] screenshotPng;
        [SerializeField] private string screenshotNote = string.Empty;
        [SerializeField] private string screenshotMime = "image/png";
        [SerializeField] private string screenshotName = PhotoFitter.BaseName + ".png";
        [SerializeField] private bool includeScreenshot = true;
        [SerializeField] private bool includeSchemeData;
        [SerializeField] private string contextInputsJson = "{}";
        [SerializeField] private string schemeJson = string.Empty;
        [SerializeField] private string statusText = string.Empty;
        [SerializeField] private SendStatus lastStatus = SendStatus.Invalid;
        [SerializeField] private bool hasStatus;
        [SerializeField] private Vector2 scroll;

        [NonSerialized] private Texture2D thumbnail;
        [NonSerialized] private Task<SendResult> sending;
        [NonSerialized] private bool focusMessage = true;

        private const string MessageControl = "SchemaFeedbackMessage";

        /// <summary>
        /// A floating utility window, which is what a dialog should be. WindowCapture's picture of a utility window
        /// (a bare HostView rather than a DockArea) is not framed correctly, so the manual verification tool turns this
        /// off to take a picture of the dialog; the capture the feature relies on is of the Schema Editor, a DockArea tab.
        /// </summary>
        internal static bool UseUtilityWindow = true;

        /// <summary>Opens the dialog with what was captured. <paramref name="screenshot"/> may be null (then <paramref name="screenshotNote"/> says why).</summary>
        internal static FeedbackWindow Open(FeedbackContextInputs inputs, Texture2D screenshot, string screenshotNote)
        {
            var window = GetWindow<FeedbackWindow>(UseUtilityWindow, "Send Schema Feedback", true);
            window.minSize = new Vector2(460, 560);
            window.contextInputsJson = JsonConvert.SerializeObject(inputs);
            window.schemeJson = inputs.SchemeJson ?? string.Empty;
            window.screenshotNote = screenshotNote ?? string.Empty;
            window.screenshotPng = null;
            window.includeScreenshot = screenshot != null;
            if (screenshot != null)
            {
                var photo = PhotoFitter.Fit(screenshot);
                window.screenshotPng = photo.Bytes;
                window.screenshotMime = photo.Mime;
                window.screenshotName = photo.Name;
            }

            window.hasStatus = false;
            window.statusText = string.Empty;
            window.ReleaseThumbnail();
            window.focusMessage = true;
            window.Show();
            window.Focus();
            return window;
        }

        private void OnDestroy() => ReleaseThumbnail();

        private void ReleaseThumbnail()
        {
            if (thumbnail != null) DestroyImmediate(thumbnail);
            thumbnail = null;
        }

        private void Update()
        {
            if (sending != null && sending.IsCompleted)
            {
                var task = sending;
                sending = null;
                var result = task.IsFaulted
                    ? new SendResult { Status = SendStatus.Queued, Message = "Sending failed unexpectedly: " + task.Exception?.GetBaseException().Message }
                    : task.Result;
                statusText = result.Message;
                lastStatus = result.Status;
                hasStatus = true;
                if (result.Status == SendStatus.Sent) message = string.Empty;
                if (result.Status == SendStatus.Sent || result.Status == SendStatus.Queued) screenshotPng = null;
                Repaint();
            }

            if (sending != null) Repaint();
        }

        private void OnGUI()
        {
            var busy = sending != null;
            var settings = FeedbackSettings.LoadFromEditorPrefs();
            var evt = Event.current;

            // Cmd/Ctrl+Enter sends. Checked before the text area so the area does not eat the Return.
            if (evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && (evt.command || evt.control))
            {
                evt.Use();
                if (!busy) StartSend(settings);
            }

            // No horizontal scrolling: content is laid out at the window's width, so long text wraps instead of widening the page.
            scroll = EditorGUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUI.skin.scrollView);
            {
                EditorGUILayout.LabelField("What is wrong?", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(busy))
                {
                    GUI.SetNextControlName(MessageControl);
                    message = EditorGUILayout.TextArea(message, GUILayout.MinHeight(90), GUILayout.ExpandWidth(true));
                    if (focusMessage && evt.type == EventType.Repaint)
                    {
                        focusMessage = false;
                        EditorGUI.FocusTextInControl(MessageControl);
                    }
                }

                EditorGUILayout.Space(6);
                DrawScreenshot(busy);

                EditorGUILayout.Space(6);
                using (new EditorGUI.DisabledScope(busy || string.IsNullOrEmpty(schemeJson)))
                {
                    includeSchemeData = EditorGUILayout.ToggleLeft(
                        "Include the open scheme's data",
                        includeSchemeData && !string.IsNullOrEmpty(schemeJson));
                }

                EditorGUILayout.LabelField(
                    string.IsNullOrEmpty(schemeJson)
                        ? "No scheme is open, so there is no data to include."
                        : "The open scheme as JSON, " + schemeJson.Length.ToString("N0") + " characters. Off by default: it is your data.",
                    EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.Space(6);
                FeedbackSettingsGui.Draw(settings, foldoutByDefault: !settings.IsConfigured);

                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(
                    "The report goes to the repairkit daemon on this machine, over its local socket. Nothing is uploaded elsewhere.",
                    MessageType.None);
            }

            EditorGUILayout.EndScrollView();

            if (hasStatus)
            {
                EditorGUILayout.HelpBox(statusText, lastStatus == SendStatus.Sent ? MessageType.Info : MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close", GUILayout.Width(80))) Close();
                using (new EditorGUI.DisabledScope(busy || string.IsNullOrWhiteSpace(message)))
                {
                    var label = busy ? "Sending..." : "Send  (" + (Application.platform == RuntimePlatform.OSXEditor ? "Cmd" : "Ctrl") + "+Enter)";
                    if (GUILayout.Button(label, GUILayout.Width(150))) StartSend(settings);
                }
            }

            EditorGUILayout.Space(4);
        }

        private void DrawScreenshot(bool busy)
        {
            if (screenshotPng == null || screenshotPng.Length == 0)
            {
                EditorGUILayout.LabelField("Screenshot", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(string.IsNullOrEmpty(screenshotNote) ? "No screenshot." : screenshotNote, EditorStyles.wordWrappedMiniLabel);
                return;
            }

            if (thumbnail == null)
            {
                thumbnail = new Texture2D(2, 2, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
                ImageConversion.LoadImage(thumbnail, screenshotPng);
            }

            using (new EditorGUI.DisabledScope(busy))
            {
                includeScreenshot = EditorGUILayout.ToggleLeft("Include a screenshot of the Schema Editor", includeScreenshot);
            }

            EditorGUILayout.LabelField(
                thumbnail.width + " x " + thumbnail.height + " px, " + (screenshotPng.Length / 1024).ToString("N0") + " KB, taken before this window opened.",
                EditorStyles.wordWrappedMiniLabel);

            // A fixed-height slot as wide as the window; ScaleToFit keeps the picture's shape inside it.
            var slot = GUILayoutUtility.GetRect(10, 150, GUILayout.ExpandWidth(true), GUILayout.Height(150));
            var aspect = thumbnail.height > 0 ? (float)thumbnail.width / thumbnail.height : 1.6f;
            using (new EditorGUI.DisabledScope(!includeScreenshot))
            {
                GUI.DrawTexture(slot, thumbnail, ScaleMode.ScaleToFit, false, aspect);
            }
        }

        private void StartSend(FeedbackSettings settings)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var inputs = JsonConvert.DeserializeObject<FeedbackContextInputs>(contextInputsJson) ?? new FeedbackContextInputs();
            inputs.SchemeJson = includeSchemeData && !string.IsNullOrEmpty(schemeJson) ? schemeJson : null;
            var draft = new FeedbackDraft
            {
                Message = message.Trim(),
                Context = FeedbackContext.Build(inputs),
                Photo = includeScreenshot && screenshotPng != null && screenshotPng.Length > 0
                    ? new FeedbackPhoto { Name = screenshotName, Mime = screenshotMime, Bytes = screenshotPng }
                    : null,
            };

            hasStatus = false;
            var sender = new FeedbackSender(settings);
            sending = Task.Run(() => sender.Send(draft));
        }
    }
}
