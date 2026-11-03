using System;
using System.IO;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback.Tests
{
    /// <summary>Shared fixtures. Tests never touch EditorPrefs: EditorPrefs is per machine, not per project.</summary>
    internal static class FeedbackTestKit
    {
        public static string NewTempDir(string tag)
        {
            var dir = Path.Combine(Path.GetTempPath(), "schema-feedback-" + tag + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void DeleteQuietly(string dir)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch (IOException)
            {
                // a leftover temp directory is not a test failure
            }
        }

        /// <summary>A texture of random bytes: PNG cannot compress it, so its encoded size tracks its pixel count.</summary>
        public static Texture2D NoiseTexture(int width, int height, int seed = 1)
        {
            var bytes = new byte[width * height * 3];
            new System.Random(seed).NextBytes(bytes);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.LoadRawTextureData(bytes);
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// A stand-in for a real screenshot of an Editor window: the dark theme's flat grey, a toolbar band, a
        /// panel and a few rows of "text". Mostly flat fill with a little detail, which is what tells a drawn
        /// window apart from noise.
        /// </summary>
        public static Texture2D WindowLikeTexture(int width = 320, int height = 200)
        {
            var background = new Color32(56, 56, 56, 255);
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = background;

            void Fill(int x0, int y0, int w, int h, Color32 color)
            {
                for (var y = y0; y < Math.Min(height, y0 + h); y++)
                {
                    for (var x = x0; x < Math.Min(width, x0 + w); x++) pixels[y * width + x] = color;
                }
            }

            Fill(0, height - height / 10, width, height / 10, new Color32(42, 42, 42, 255)); // toolbar
            Fill(width / 20, height / 8, width / 3, height / 2, new Color32(72, 72, 72, 255)); // a panel
            for (var row = 0; row < 6; row++) // rows of "text": thin light runs, a small part of the picture
            {
                Fill(width / 10, height / 4 + row * height / 12, width / 5, Math.Max(1, height / 80), new Color32(200, 200, 200, 255));
            }

            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// What report 67d24d9a attached: a black frame with a band of graphics-memory noise across the top,
        /// <paramref name="noiseShare"/> of the height. Not all black, so an all-or-nothing check passes it.
        /// </summary>
        public static Texture2D BlackWithNoiseBandTexture(int width = 640, int height = 400, float noiseShare = 0.05f, int seed = 3)
        {
            var random = new System.Random(seed);
            var bandRows = Math.Max(1, (int)Math.Round(height * noiseShare));
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                var noisy = y >= height - bandRows; // texture row 0 is the bottom, so the band is on top
                for (var x = 0; x < width; x++)
                {
                    pixels[y * width + x] = noisy
                        ? new Color32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255)
                        : new Color32(0, 0, 0, 255);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        public static Texture2D SolidTexture(int width, int height, Color32 color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        public static FeedbackPhoto PngPhoto(int width = 64, int height = 48)
        {
            var texture = SolidTexture(width, height, new Color32(40, 120, 200, 255));
            try
            {
                return new FeedbackPhoto
                {
                    Name = "schema-editor.png",
                    Mime = "image/png",
                    Bytes = ImageConversion.EncodeToPNG(texture),
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
