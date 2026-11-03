using System;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>
    /// Makes a screenshot fit repairkit's 2 MB photo limit, and never refuses.
    /// <list type="number">
    /// <item>A PNG that fits goes out unchanged (<c>schema-editor.png</c>).</item>
    /// <item>Otherwise the image is downscaled to 75% and encoded as JPEG (quality 85); if that is still over,
    /// it is downscaled again (75% each step) and the quality steps down to 50, until it fits.</item>
    /// <item>Below 160 px wide the loop stops at quality 40, which fits any image of that size.</item>
    /// </list>
    /// The result is <c>schema-editor.jpg</c> when it had to be re-encoded. Pure CPU work (a box filter over
    /// <c>GetPixels32</c>), so it needs no GPU and runs in batch mode.
    /// </summary>
    internal static class PhotoFitter
    {
        public const int MaxBytes = 2 * 1024 * 1024;
        public const string BaseName = "schema-editor";

        private const float StepScale = 0.75f;
        private const int FirstJpegQuality = 85;
        private const int LowestJpegQuality = 50;
        private const int FloorWidth = 160;

        public static FeedbackPhoto Fit(Texture2D source, int maxBytes = MaxBytes)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var png = ImageConversion.EncodeToPNG(source);
            if (png != null && png.Length > 0 && png.Length <= maxBytes)
            {
                return new FeedbackPhoto { Name = BaseName + ".png", Mime = "image/png", Bytes = png };
            }

            var pixels = source.GetPixels32();
            var width = source.width;
            var height = source.height;
            var quality = FirstJpegQuality;
            byte[] jpeg = null;

            while (true)
            {
                var targetWidth = Math.Max(1, (int)Math.Round(width * StepScale));
                var targetHeight = Math.Max(1, (int)Math.Round(height * StepScale));
                if (targetWidth == width && targetHeight == height) break; // 1x1: cannot shrink further

                pixels = BoxDownscale(pixels, width, height, targetWidth, targetHeight);
                width = targetWidth;
                height = targetHeight;

                jpeg = EncodeJpeg(pixels, width, height, quality);
                if (jpeg.Length <= maxBytes) break;

                quality = Math.Max(LowestJpegQuality, quality - 5);
                if (width <= FloorWidth)
                {
                    jpeg = EncodeJpeg(pixels, width, height, 40);
                    break;
                }
            }

            return new FeedbackPhoto { Name = BaseName + ".jpg", Mime = "image/jpeg", Bytes = jpeg };
        }

        /// <summary>A pixel this dark in every channel counts as black; the dark Editor theme sits near 56.</summary>
        private const int DarkLevel = 8;

        /// <summary>At least this share of the picture being black means it is not a window.</summary>
        internal const float BlackShare = 0.9f;

        /// <summary>Neighbouring pixels further apart than this on any channel count as an edge.</summary>
        private const int NeighbourJump = 40;

        /// <summary>At least this share of neighbouring pairs being edges means noise, not a drawn window.</summary>
        internal const float NoiseShare = 0.5f;

        private const int SampleBudget = 8192;

        /// <summary>
        /// Why this capture is not worth sending, in words for the person, or null when it looks like a window.
        /// Two ways a capture goes wrong, both seen in the wild:
        /// <list type="bullet">
        /// <item>Black, or all but black -- a locked screen, or a view that did not draw before it was grabbed.
        /// Checked as a share, not as "every pixel": the report this came from was a black frame with a band of
        /// garbage across the top, which an all-or-nothing check let through.</item>
        /// <item>Noise -- graphics memory that was never written, which reads back as random colour. A drawn
        /// window is mostly flat fill, so almost no two neighbouring pixels are far apart; noise is the opposite.</item>
        /// </list>
        /// Sampled on a coarse grid, so the cost does not grow with the window.
        /// </summary>
        public static string DescribeUnusable(Texture2D texture)
        {
            const string empty = "The capture came out empty";
            if (texture == null || texture.width < 1 || texture.height < 1) return empty;

            var pixels = texture.GetPixels32();
            if (pixels.Length == 0) return empty;

            var width = texture.width;
            var stride = Math.Max(1, pixels.Length / SampleBudget);
            // A stride that divides the width walks the same few columns down the whole picture, which for a
            // power-of-two sized window would judge it on a sliver. One more pixel breaks the alignment.
            if (stride > 1 && width % stride == 0) stride++;

            int sampled = 0, black = 0, pairs = 0, edges = 0;
            for (var i = 0; i < pixels.Length; i += stride)
            {
                var p = pixels[i];
                sampled++;
                if (p.r <= DarkLevel && p.g <= DarkLevel && p.b <= DarkLevel) black++;

                if (i % width == width - 1) continue; // the row's last pixel has no right-hand neighbour
                var right = pixels[i + 1];
                pairs++;
                if (Math.Abs(p.r - right.r) > NeighbourJump || Math.Abs(p.g - right.g) > NeighbourJump ||
                    Math.Abs(p.b - right.b) > NeighbourJump) edges++;
            }

            if (sampled == 0) return empty;
            if (black >= sampled * BlackShare)
            {
                return black == sampled ? "The capture came out black" : "The capture came out all but black";
            }

            if (pairs > 0 && edges >= pairs * NoiseShare) return "The capture came out as noise, not a picture of the window";

            // Neither alone, but nothing else either: a part-drawn grab is black where it drew nothing and noise
            // where the memory was never written, and a drawn window is overwhelmingly neither.
            if (black + edges >= sampled * BlackShare) return "The capture came out black with noise across it";
            return null;
        }

        private static byte[] EncodeJpeg(Color32[] pixels, int width, int height, int quality)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false);
                return ImageConversion.EncodeToJPG(texture, quality);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Area-averaging downscale: every target pixel is the mean of the source pixels it covers.</summary>
        internal static Color32[] BoxDownscale(Color32[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            var target = new Color32[targetWidth * targetHeight];
            for (var ty = 0; ty < targetHeight; ty++)
            {
                var y0 = ty * sourceHeight / targetHeight;
                var y1 = Math.Max(y0 + 1, (ty + 1) * sourceHeight / targetHeight);
                for (var tx = 0; tx < targetWidth; tx++)
                {
                    var x0 = tx * sourceWidth / targetWidth;
                    var x1 = Math.Max(x0 + 1, (tx + 1) * sourceWidth / targetWidth);

                    long r = 0, g = 0, b = 0, a = 0;
                    for (var y = y0; y < y1; y++)
                    {
                        var row = y * sourceWidth;
                        for (var x = x0; x < x1; x++)
                        {
                            var p = source[row + x];
                            r += p.r;
                            g += p.g;
                            b += p.b;
                            a += p.a;
                        }
                    }

                    var count = (x1 - x0) * (y1 - y0);
                    target[ty * targetWidth + tx] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), (byte)(a / count));
                }
            }

            return target;
        }
    }
}
