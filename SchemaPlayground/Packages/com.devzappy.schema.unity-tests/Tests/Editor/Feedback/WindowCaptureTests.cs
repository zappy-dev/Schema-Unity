using NUnit.Framework;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback.Tests
{
    /// <summary>
    /// The pure part of the window capture: turning the grabbed host view into an upright content picture.
    /// The part that needs a real window and a GPU is <see cref="WindowCaptureVerify"/>, run by hand.
    /// </summary>
    public class WindowCaptureTests
    {
        private Texture2D grabbed;
        private Texture2D result;

        [TearDown]
        public void TearDown()
        {
            if (grabbed != null) Object.DestroyImmediate(grabbed);
            if (result != null) Object.DestroyImmediate(result);
        }

        // 8 x 6 host view: a 2-row dock tab strip plus a 4-row content area. Content row k from the top is red = 10 + 20k.
        private const int Width = 8;
        private const int HostHeight = 6;
        private const int StripRows = 2;
        private const int ContentRows = HostHeight - StripRows;

        private static byte ContentRed(int fromTop) => (byte)(10 + 20 * fromTop);

        [Test]
        public void When_the_grab_is_upside_down_the_content_comes_out_upright_without_the_tab_strip()
        {
            // Texture row y holds host row y counted from the top: strip first, then content top to bottom.
            grabbed = new Texture2D(Width, HostHeight, TextureFormat.RGB24, false);
            for (var y = 0; y < HostHeight; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    grabbed.SetPixel(x, y, y < StripRows ? new Color32(200, 200, 200, 255) : new Color32(ContentRed(y - StripRows), 0, 0, 255));
                }
            }

            grabbed.Apply(false);

            result = WindowCapture.FlipAndCrop(grabbed, Width, ContentRows, flip: true);

            Assert.That(result.width, Is.EqualTo(Width));
            Assert.That(result.height, Is.EqualTo(ContentRows));
            // texture row 0 is the bottom: it must be the content's last row, and the top row its first
            Assert.That(result.GetPixel(0, ContentRows - 1), Is.EqualTo((Color)new Color32(ContentRed(0), 0, 0, 255)), "top row is the content's first");
            Assert.That(result.GetPixel(0, 0), Is.EqualTo((Color)new Color32(ContentRed(ContentRows - 1), 0, 0, 255)), "bottom row is the content's last");
            for (var y = 0; y < ContentRows; y++)
            {
                Assert.That(result.GetPixel(3, y).r, Is.Not.EqualTo(200 / 255f), "no tab strip row survives (row " + y + ")");
            }
        }

        [Test]
        public void When_the_grab_is_already_upright_only_the_tab_strip_is_cropped_off()
        {
            // Bottom-up: content rows first (row 0 is the content's bottom), the strip on top.
            grabbed = new Texture2D(Width, HostHeight, TextureFormat.RGB24, false);
            for (var y = 0; y < HostHeight; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    grabbed.SetPixel(x, y, y >= ContentRows ? new Color32(200, 200, 200, 255) : new Color32((byte)(10 + 20 * y), 0, 0, 255));
                }
            }

            grabbed.Apply(false);

            result = WindowCapture.FlipAndCrop(grabbed, Width, ContentRows, flip: false);

            Assert.That(result.height, Is.EqualTo(ContentRows));
            for (var y = 0; y < ContentRows; y++)
            {
                Assert.That(result.GetPixel(0, y), Is.EqualTo((Color)new Color32((byte)(10 + 20 * y), 0, 0, 255)));
            }
        }

        [Test]
        public void A_window_that_is_not_showing_is_reported_not_thrown()
        {
            var window = ScriptableObject.CreateInstance<UnityEditor.EditorWindow>();
            try
            {
                var capture = WindowCapture.Capture(window); // never Show()n: no host view

                Assert.That(capture.Ok, Is.False);
                Assert.That(capture.Texture, Is.Null);
                Assert.That(capture.Problem, Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void No_window_at_all_is_reported_not_thrown()
        {
            var capture = WindowCapture.Capture(null);

            Assert.That(capture.Ok, Is.False);
            Assert.That(capture.Problem, Is.Not.Empty);
        }
    }
}
