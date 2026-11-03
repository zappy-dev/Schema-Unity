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
    /// Two things keep the picture honest. The view is told to draw itself (<c>RepaintImmediately</c>) right before
    /// each grab, because a queued <c>Repaint()</c> may not have been served yet in a busy Editor; and the render
    /// texture is cleared to black first, because GrabPixels leaves anything it does not draw as whatever was last
    /// in that graphics memory. Without both, a view that had not drawn came back as a black frame with a band of
    /// coloured noise across it, and was sent as if it were a screenshot. What still comes back blank or noisy is
    /// refused by <see cref="PhotoFitter.DescribeUnusable"/> and the dialog says so.
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

        /// <summary>How many times to draw-and-grab before giving up; the second try costs one more repaint.</summary>
        private const int Attempts = 2;

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

                var drawNow = guiViewType.GetMethod("RepaintImmediately", Any, null, Type.EmptyTypes, null);

                var viewRect = ReadViewRect(view);
                var windowSize = window.position.size;
                if (viewRect.width < 1 || viewRect.height < 1) return Fail("The window has no size yet.");

                var scale = scaleOverride ?? EditorGUIUtility.pixelsPerPoint;
                var pixelWidth = Mathf.RoundToInt(viewRect.width * scale);
                var pixelHeight = Mathf.RoundToInt(viewRect.height * scale);
                if ((long)pixelWidth * pixelHeight > MaxPixels) return Fail("The window is too large to capture.");

                var contentWidth = Mathf.Min(pixelWidth, Mathf.RoundToInt(windowSize.x * scale));
                var contentHeight = Mathf.Min(pixelHeight, Mathf.RoundToInt(windowSize.y * scale));

                string problem = null;
                for (var attempt = 1; attempt <= Attempts; attempt++)
                {
                    // Draw the view now, synchronously. A queued Repaint() is served whenever the Editor gets to it,
                    // which in a large project can be several EditorApplication.update ticks away; GrabPixels would
                    // then read back a region the view has not drawn into, which is where the black-with-noise
                    // screenshots came from.
                    DrawNow(view, drawNow);

                    var full = Grab(view, grab, viewRect, pixelWidth, pixelHeight, out problem);
                    if (full == null) continue;

                    try
                    {
                        var upright = FlipAndCrop(full, contentWidth, contentHeight, flip: SystemInfo.graphicsUVStartsAtTop);
                        problem = PhotoFitter.DescribeUnusable(upright);
                        if (problem == null) return new CaptureResult { Texture = upright };
                        UnityEngine.Object.DestroyImmediate(upright);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(full);
                    }
                }

                return Fail(problem + ", so it was left out.");
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
                return Fail("The window could not be captured (" + inner.GetType().Name + ": " + inner.Message + ").");
            }
        }

        /// <summary>Asks the host view to draw itself at once, if this Unity version lets us. Best effort: a window that will not draw is caught by the blank check.</summary>
        private static void DrawNow(object view, MethodInfo drawNow)
        {
            if (drawNow == null) return;
            try
            {
                drawNow.Invoke(view, null);
            }
            catch (Exception)
            {
                // Drawing on demand is an optimisation of the odds, never the thing that decides the capture.
            }
        }

        /// <param name="problem">Why there is no texture, when null comes back.</param>
        private static Texture2D Grab(object view, MethodInfo grab, Rect viewRect, int pixelWidth, int pixelHeight, out string problem)
        {
            problem = null;
            var target = new RenderTexture(pixelWidth, pixelHeight, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                if (!target.Create())
                {
                    problem = "There was not enough graphics memory for a picture of a window this large";
                    return null;
                }

                // Start from a known black. A fresh RenderTexture holds whatever was last in that graphics memory,
                // and GrabPixels leaves every part it does not draw as it found it, so an incomplete grab came back
                // as bands of coloured noise that read as a real (and badly corrupt) screenshot.
                RenderTexture.active = target;
                GL.Clear(true, true, Color.black);

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
