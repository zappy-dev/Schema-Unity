using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback
{
    internal sealed class CaptureResult
    {
        public Texture2D Texture;

        /// <summary>Why there is no usable picture, in words for the person. Null when <see cref="Texture"/> is good.</summary>
        public string Problem;

        public bool Ok => Texture != null && Problem == null;
    }

    /// <summary>
    /// Takes a picture of one EditorWindow's own view, not of the screen. The window's host view
    /// (<c>EditorWindow.m_Parent</c>, a <c>UnityEditor.GUIView</c>) renders itself into a RenderTexture through
    /// its internal <c>GrabPixels(RenderTexture, Rect)</c>, found by reflection. That route needs no screen
    /// coordinates (so no origin to calibrate and no macOS Screen Recording permission), cannot pick up another
    /// window that overlaps this one, and works with the screen locked.
    /// <para>
    /// Facts it rests on (Unity 6000.6, Metal, Retina): the render texture must be sized in pixels
    /// (view points x <c>pixelsPerPoint</c>); the picture comes back upside down, and includes the dock tab strip
    /// above the content, so the rows are flipped and the strip is cropped off.
    /// </para>
    /// <para>
    /// There is deliberately no fallback to <c>InternalEditorUtility.ReadScreenPixel</c>: with the screen locked
    /// it fails an engine assertion (<c>image != nil</c>) and aborts the whole Editor, which is not a price a
    /// feedback button may charge. If GrabPixels cannot be found in some Unity version, the screenshot is simply
    /// left out and the report says why.
    /// </para>
    /// </summary>
    internal static class WindowCapture
    {
        private const int MaxPixels = 8192 * 8192;
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static CaptureResult Capture(EditorWindow window) => Capture(window, null);

        /// <param name="scaleOverride">Pixels per point to render at instead of the Editor's; for diagnosing a window whose scale differs.</param>
        internal static CaptureResult Capture(EditorWindow window, float? scaleOverride)
        {
            if (window == null) return Fail("There is no window to capture.");

            try
            {
                var view = typeof(EditorWindow).GetField("m_Parent", Any)?.GetValue(window);
                if (view == null) return Fail("The window is not showing, so there is nothing to capture.");

                var guiViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
                var grab = guiViewType?.GetMethod("GrabPixels", Any, null, new[] { typeof(RenderTexture), typeof(Rect) }, null);
                if (grab == null) return Fail("This Unity version has no way to capture a window; the screenshot was left out.");

                var viewRect = ReadViewRect(view);
                var windowSize = window.position.size;
                if (viewRect.width < 1 || viewRect.height < 1) return Fail("The window has no size yet.");

                var scale = scaleOverride ?? EditorGUIUtility.pixelsPerPoint;
                var pixelWidth = Mathf.RoundToInt(viewRect.width * scale);
                var pixelHeight = Mathf.RoundToInt(viewRect.height * scale);
                if ((long)pixelWidth * pixelHeight > MaxPixels) return Fail("The window is too large to capture.");

                var full = Grab(view, grab, viewRect, pixelWidth, pixelHeight);
                try
                {
                    var contentWidth = Mathf.Min(pixelWidth, Mathf.RoundToInt(windowSize.x * scale));
                    var contentHeight = Mathf.Min(pixelHeight, Mathf.RoundToInt(windowSize.y * scale));
                    var upright = FlipAndCrop(full, contentWidth, contentHeight, flip: SystemInfo.graphicsUVStartsAtTop);

                    if (PhotoFitter.LooksBlack(upright))
                    {
                        UnityEngine.Object.DestroyImmediate(upright);
                        return Fail("The capture came out black, so it was left out.");
                    }

                    return new CaptureResult { Texture = upright };
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(full);
                }
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
                return Fail("The window could not be captured (" + inner.GetType().Name + ": " + inner.Message + ").");
            }
        }

        private static Texture2D Grab(object view, MethodInfo grab, Rect viewRect, int pixelWidth, int pixelHeight)
        {
            var target = new RenderTexture(pixelWidth, pixelHeight, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                grab.Invoke(view, new object[] { target, new Rect(0, 0, viewRect.width, viewRect.height) });
                RenderTexture.active = target;
                var texture = new Texture2D(pixelWidth, pixelHeight, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, pixelWidth, pixelHeight), 0, 0);
                texture.Apply(false);
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// Turns the grabbed host view into its content area, upright, as an ordinary bottom-up texture.
        /// With <paramref name="flip"/> (what Metal, D3D and Vulkan produce, checked on Metal) texture row y holds the
        /// host view's row y counted from the top, so the dock tab strip is rows [0, strip) and the content
        /// is the rest, upside down. Without it the texture is already bottom-up, tab strip on top.
        /// </summary>
        internal static Texture2D FlipAndCrop(Texture2D grabbed, int contentWidth, int contentHeight, bool flip)
        {
            var width = grabbed.width;
            var height = grabbed.height;
            var source = grabbed.GetPixels32();
            var result = new Color32[contentWidth * contentHeight];

            for (var y = 0; y < contentHeight; y++)
            {
                var sourceRow = flip ? height - 1 - y : y;
                Array.Copy(source, sourceRow * width, result, y * contentWidth, contentWidth);
            }

            var texture = new Texture2D(contentWidth, contentHeight, TextureFormat.RGB24, false);
            texture.SetPixels32(result);
            texture.Apply(false);
            return texture;
        }

        private static Rect ReadViewRect(object view)
        {
            var property = view.GetType().GetProperty("position", Any);
            return property != null && property.GetValue(view) is Rect rect ? rect : Rect.zero;
        }

        private static CaptureResult Fail(string problem) => new CaptureResult { Problem = problem };
    }
}
