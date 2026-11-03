using System;
using Newtonsoft.Json.Linq;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>One image for a report, already small enough to send (see <see cref="PhotoFitter"/>).</summary>
    internal sealed class FeedbackPhoto
    {
        public string Name;
        public string Mime;
        public byte[] Bytes;

        /// <summary>The form repairkit's <c>attach_photos</c> takes: <c>data:image/png;base64,...</c>.</summary>
        public string ToDataUrl() => "data:" + Mime + ";base64," + Convert.ToBase64String(Bytes);
    }

    /// <summary>What a person filed: the words, where they came from, the host state, and at most one image.</summary>
    internal sealed class FeedbackDraft
    {
        public const string DefaultSource = "schema-editor";

        public string Message;
        public string Source = DefaultSource;
        public JObject Context = new JObject();
        public FeedbackPhoto Photo;
    }

    internal enum SendStatus
    {
        /// <summary>The daemon has the report.</summary>
        Sent,

        /// <summary>The daemon could not be reached; the report is in the outbox and goes out on the next connect.</summary>
        Queued,

        /// <summary>No socket path is set on this machine, so nothing was sent and nothing was written.</summary>
        NotConfigured,

        /// <summary>The draft cannot be sent as it is (no message).</summary>
        Invalid,
    }

    internal sealed class SendResult
    {
        public SendStatus Status;
        public string ReportId;

        /// <summary>A sentence for the person, in plain words.</summary>
        public string Message;

        /// <summary>The technical reason, when there is one (for the Console).</summary>
        public string Error;
    }

    /// <summary>The table cell that had focus, as the person would name it.</summary>
    internal sealed class FeedbackCell
    {
        public string Attribute;
        public int Row;
        public string Value;
    }
}
