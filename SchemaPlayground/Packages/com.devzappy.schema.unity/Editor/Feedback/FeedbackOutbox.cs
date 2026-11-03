using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>One report waiting for a daemon: what it says, which step of delivery it has passed, and its image.</summary>
    internal sealed class OutboxEntry
    {
        public string Directory;
        public string Message;
        public string Source = FeedbackDraft.DefaultSource;
        public JObject Context = new JObject();
        public string CreatedUtc;

        /// <summary>The id the daemon gave the report once <c>submit</c> succeeded; the retry then only attaches the image.</summary>
        public string SubmittedId;

        public int Attempts;
        public string LastError;

        /// <summary>The image, when the report has one that is not yet attached.</summary>
        public FeedbackPhoto Photo;

        public FeedbackDraft ToDraft() => new FeedbackDraft { Message = Message, Source = Source, Context = Context, Photo = Photo };
    }

    /// <summary>
    /// Reports that could not be delivered, one directory each under the root:
    /// <c>&lt;timestamp&gt;-&lt;id&gt;/report.json</c> plus <c>schema-editor.png</c> or <c>.jpg</c>. The root is
    /// <c>~/.schema-unity/feedback-outbox</c> by default, outside every project. Directory names sort oldest first.
    /// An entry is deleted only when the daemon has taken both the report and its image.
    /// </summary>
    internal sealed class FeedbackOutbox
    {
        private const string ReportFile = "report.json";

        private static readonly object ClockGate = new object();
        private static long lastTicks;

        private readonly string root;

        public FeedbackOutbox(string root)
        {
            this.root = root;
        }

        public string Root => root;

        public bool HasAnyEntries()
        {
            try
            {
                return System.IO.Directory.Exists(root) && System.IO.Directory.GetDirectories(root).Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public OutboxEntry Enqueue(FeedbackDraft draft, string lastError)
        {
            var entry = new OutboxEntry
            {
                Directory = Path.Combine(root, NewEntryName()),
                Message = draft.Message,
                Source = draft.Source,
                Context = draft.Context ?? new JObject(),
                CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                Photo = draft.Photo,
                LastError = lastError,
            };
            Save(entry);
            return entry;
        }

        /// <summary>Writes (or rewrites) the entry's report file, and its image when it still has one.</summary>
        public void Save(OutboxEntry entry)
        {
            System.IO.Directory.CreateDirectory(entry.Directory);
            var report = new JObject
            {
                ["message"] = entry.Message,
                ["source"] = entry.Source,
                ["context"] = entry.Context,
                ["created_utc"] = entry.CreatedUtc,
                ["submitted_id"] = entry.SubmittedId,
                ["attempts"] = entry.Attempts,
                ["last_error"] = entry.LastError,
                ["photo_name"] = entry.Photo?.Name,
                ["photo_mime"] = entry.Photo?.Mime,
            };

            if (entry.Photo != null) File.WriteAllBytes(Path.Combine(entry.Directory, entry.Photo.Name), entry.Photo.Bytes);

            // report.json goes last and via a temp name, so a crash never leaves a report that names a missing image
            var temp = Path.Combine(entry.Directory, ReportFile + ".tmp");
            File.WriteAllText(temp, report.ToString(Formatting.Indented));
            var final = Path.Combine(entry.Directory, ReportFile);
            if (File.Exists(final)) File.Delete(final);
            File.Move(temp, final);
        }

        /// <summary>Entries oldest first. A directory that cannot be read is skipped and left alone.</summary>
        public List<OutboxEntry> Pending()
        {
            var entries = new List<OutboxEntry>();
            if (!System.IO.Directory.Exists(root)) return entries;

            var directories = System.IO.Directory.GetDirectories(root);
            Array.Sort(directories, StringComparer.Ordinal);
            foreach (var directory in directories)
            {
                var entry = TryLoad(directory);
                if (entry != null) entries.Add(entry);
            }

            return entries;
        }

        public void Remove(OutboxEntry entry)
        {
            try
            {
                if (System.IO.Directory.Exists(entry.Directory)) System.IO.Directory.Delete(entry.Directory, true);
            }
            catch (IOException)
            {
                // delivered already; a leftover directory is retried (and would only fail on the id) next time
            }
        }

        private static OutboxEntry TryLoad(string directory)
        {
            try
            {
                var file = Path.Combine(directory, ReportFile);
                if (!File.Exists(file)) return null;

                var report = JObject.Parse(File.ReadAllText(file));
                var entry = new OutboxEntry
                {
                    Directory = directory,
                    Message = (string)report["message"],
                    Source = (string)report["source"] ?? FeedbackDraft.DefaultSource,
                    Context = report["context"] as JObject ?? new JObject(),
                    CreatedUtc = (string)report["created_utc"],
                    SubmittedId = (string)report["submitted_id"],
                    Attempts = (int?)report["attempts"] ?? 0,
                    LastError = (string)report["last_error"],
                };

                var photoName = (string)report["photo_name"];
                if (!string.IsNullOrEmpty(photoName))
                {
                    var photoFile = Path.Combine(directory, Path.GetFileName(photoName));
                    if (File.Exists(photoFile))
                    {
                        entry.Photo = new FeedbackPhoto
                        {
                            Name = Path.GetFileName(photoName),
                            Mime = (string)report["photo_mime"] ?? "image/png",
                            Bytes = File.ReadAllBytes(photoFile),
                        };
                    }
                }

                return string.IsNullOrWhiteSpace(entry.Message) ? null : entry;
            }
            catch (Exception e) when (e is JsonException || e is IOException || e is InvalidCastException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string NewEntryName()
        {
            long ticks;
            lock (ClockGate)
            {
                ticks = Math.Max(DateTime.UtcNow.Ticks, lastTicks + TimeSpan.TicksPerMillisecond);
                lastTicks = ticks;
            }

            var stamp = new DateTime(ticks, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmssfff", CultureInfo.InvariantCulture);
            return stamp + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }
    }
}
