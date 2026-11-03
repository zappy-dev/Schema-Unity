using System;
using NUnit.Framework;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback.Tests
{
    /// <summary>A1.4: a screenshot over 2 MB is downscaled, then sent as JPEG, never refused.</summary>
    public class PhotoFitterTests
    {
        private Texture2D texture;
        private Texture2D decoded;

        [TearDown]
        public void TearDown()
        {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            if (decoded != null) UnityEngine.Object.DestroyImmediate(decoded);
        }

        [Test]
        public void The_cap_is_two_mebibytes()
        {
            Assert.That(PhotoFitter.MaxBytes, Is.EqualTo(2 * 1024 * 1024));
        }

        [Test]
        public void A_small_screenshot_goes_out_unchanged_as_png()
        {
            texture = FeedbackTestKit.NoiseTexture(400, 300); // ~360 kB of noise, well under the cap

            var photo = PhotoFitter.Fit(texture);

            Assert.That(photo.Mime, Is.EqualTo("image/png"));
            Assert.That(photo.Name, Is.EqualTo("schema-editor.png"));
            Assert.That(photo.Bytes.Length, Is.LessThanOrEqualTo(PhotoFitter.MaxBytes));
            decoded = new Texture2D(2, 2);
            Assert.That(ImageConversion.LoadImage(decoded, photo.Bytes), Is.True);
            Assert.That(decoded.width, Is.EqualTo(400));
            Assert.That(decoded.height, Is.EqualTo(300));
        }

        [Test]
        public void A_screenshot_over_the_cap_is_downscaled_then_sent_as_jpeg_and_never_refused()
        {
            // 3000 x 2000 noise is about 18 MB raw and does not compress as PNG: far over 2 MB.
            texture = FeedbackTestKit.NoiseTexture(3000, 2000);
            Assert.That(ImageConversion.EncodeToPNG(texture).Length, Is.GreaterThan(PhotoFitter.MaxBytes),
                "the fixture must be over the cap as a PNG or this test proves nothing");

            var photo = PhotoFitter.Fit(texture);

            Assert.That(photo, Is.Not.Null);
            Assert.That(photo.Bytes.Length, Is.LessThanOrEqualTo(PhotoFitter.MaxBytes));
            Assert.That(photo.Mime, Is.EqualTo("image/jpeg"));
            Assert.That(photo.Name, Is.EqualTo("schema-editor.jpg"));
            Assert.That(photo.Bytes[0], Is.EqualTo(0xFF), "JPEG magic");
            Assert.That(photo.Bytes[1], Is.EqualTo(0xD8), "JPEG magic");

            decoded = new Texture2D(2, 2);
            Assert.That(ImageConversion.LoadImage(decoded, photo.Bytes), Is.True, "the result must decode");
            Assert.That(decoded.width, Is.LessThan(3000), "downscaled");
            Assert.That(decoded.width / (float)decoded.height, Is.EqualTo(1.5f).Within(0.02f), "aspect kept");
        }

        [Test]
        public void The_data_url_carries_the_mime_and_the_bytes()
        {
            texture = FeedbackTestKit.NoiseTexture(3000, 2000, seed: 7);
            var photo = PhotoFitter.Fit(texture);

            var url = photo.ToDataUrl();

            Assert.That(url, Does.StartWith("data:image/jpeg;base64,"));
            var payload = Convert.FromBase64String(url.Substring(url.IndexOf(',') + 1));
            Assert.That(payload, Is.EqualTo(photo.Bytes));
        }

        [Test]
        public void A_black_capture_is_recognised_and_a_real_one_is_not()
        {
            texture = FeedbackTestKit.SolidTexture(64, 64, new Color32(0, 0, 0, 255));
            Assert.That(PhotoFitter.LooksBlack(texture), Is.True);

            UnityEngine.Object.DestroyImmediate(texture);
            texture = FeedbackTestKit.NoiseTexture(64, 64);
            Assert.That(PhotoFitter.LooksBlack(texture), Is.False);
        }
    }
}
