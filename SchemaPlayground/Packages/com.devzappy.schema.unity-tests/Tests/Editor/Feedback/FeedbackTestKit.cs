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
