using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>
    /// Delivers a draft: submit, then attach the image. An unreachable daemon, or any failure on the way, leaves
    /// the report in the outbox with the id it already has (so a retry never files it twice), and the next
    /// <see cref="Send"/> or <see cref="FlushOutbox"/> carries on. Nothing here reads EditorPrefs or the project:
    /// it works on the <see cref="FeedbackSettings"/> it is given, and with no socket path it does nothing at
    /// all, not even write to the outbox. Calls block up to the settings' timeout per op: call off the main thread.
    /// </summary>
    internal sealed class FeedbackSender
    {
        private static readonly object DeliveryGate = new object();

        private readonly FeedbackSettings settings;
        private readonly FeedbackOutbox outbox;

        public FeedbackSender(FeedbackSettings settings)
        {
            this.settings = settings;
            outbox = new FeedbackOutbox(settings.OutboxRoot);
        }

        public SendResult Send(FeedbackDraft draft)
        {
            if (!settings.IsConfigured)
            {
                return new SendResult { Status = SendStatus.NotConfigured, Message = FeedbackSettings.HowToConfigure };
            }

            if (draft == null || string.IsNullOrWhiteSpace(draft.Message))
            {
                return new SendResult { Status = SendStatus.Invalid, Message = "Write what is wrong first; a report needs a message." };
            }

            lock (DeliveryGate)
            {
                // Older reports first, so they arrive in the order they were written. If the daemon is down
                // the flush finds out, and this report goes straight to the outbox without a second attempt.
                var daemonUp = TryFlush(out _);
                return daemonUp ? Deliver(draft) : Queue(draft, "no answer on the socket");
            }
        }

        /// <summary>Sends what is waiting in the outbox; returns how many reports were completed. No socket path, or nothing queued: no-op.</summary>
        public int FlushOutbox()
        {
            if (!settings.IsConfigured) return 0;
            lock (DeliveryGate)
            {
                TryFlush(out var delivered);
                return delivered;
            }
        }

        /// <returns>False when the daemon could not be reached (so there is no point trying more).</returns>
        private bool TryFlush(out int delivered)
        {
            delivered = 0;
            if (!outbox.HasAnyEntries()) return true;

            foreach (var entry in outbox.Pending())
            {
                var outcome = Attempt(entry, out var error);
                if (outcome == Outcome.Done)
                {
                    outbox.Remove(entry);
                    delivered++;
                    continue;
                }

                entry.Attempts++;
                entry.LastError = error;
                outbox.Save(entry);
                if (outcome == Outcome.Unreachable) return false;
            }

            return true;
        }

        private SendResult Deliver(FeedbackDraft draft)
        {
            var entry = new OutboxEntry
            {
                Message = draft.Message.Trim(),
                Source = string.IsNullOrWhiteSpace(draft.Source) ? FeedbackDraft.DefaultSource : draft.Source,
                Context = draft.Context ?? new JObject(),
                Photo = draft.Photo,
                CreatedUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            };

            var outcome = Attempt(entry, out var error);
            if (outcome == Outcome.Done)
            {
                return new SendResult
                {
                    Status = SendStatus.Sent,
                    ReportId = entry.SubmittedId,
                    Message = "Sent. Report " + entry.SubmittedId + (draft.Photo != null ? " (with the screenshot)." : "."),
                };
            }

            if (entry.SubmittedId != null)
            {
                // The report is filed; only its screenshot is outstanding. Keep that for the next connect.
                var kept = outbox.Enqueue(entry.ToDraft(), error);
                kept.SubmittedId = entry.SubmittedId;
                kept.Attempts = 1;
                outbox.Save(kept);
                return new SendResult
                {
                    Status = SendStatus.Sent,
                    ReportId = entry.SubmittedId,
                    Error = error,
                    Message = "Sent as report " + entry.SubmittedId + ", but the screenshot could not be attached (" + error +
                              "). The screenshot is kept in the outbox and goes out on the next connect.",
                };
            }

            return Queue(draft, error, outcome == Outcome.Unreachable);
        }

        private SendResult Queue(FeedbackDraft draft, string error, bool unreachable = true)
        {
            var entry = outbox.Enqueue(new FeedbackDraft
            {
                Message = draft.Message.Trim(),
                Source = string.IsNullOrWhiteSpace(draft.Source) ? FeedbackDraft.DefaultSource : draft.Source,
                Context = draft.Context ?? new JObject(),
                Photo = draft.Photo,
            }, error);
            entry.Attempts = 1;
            outbox.Save(entry);
            return new SendResult
            {
                Status = SendStatus.Queued,
                Error = error,
                Message = (unreachable ? "The daemon could not be reached (" : "The daemon did not take the report (") + error +
                          "). Your report was saved to the outbox at " + entry.Directory +
                          " and goes out the next time a send connects.",
            };
        }

        private enum Outcome
        {
            Done,
            Unreachable,
            Failed,
        }

        /// <summary>Runs the remaining steps of one report: submit if it has no id, then attach its image if it still has one.</summary>
        private Outcome Attempt(OutboxEntry entry, out string error)
        {
            error = null;
            var client = new RepairkitClient(settings.SocketPath, settings.TimeoutMs);
            try
            {
                if (entry.SubmittedId == null)
                {
                    var context = entry.Context != null ? (JObject)entry.Context.DeepClone() : new JObject();
                    context["hold_for_review"] = FeedbackContext.HoldForReview;
                    entry.SubmittedId = client.Submit(entry.Message, entry.Source, context);
                    if (entry.Directory != null) outbox.Save(entry); // remember the id before the next step can fail
                }

                if (entry.Photo != null)
                {
                    try
                    {
                        client.AttachPhotos(entry.SubmittedId, new List<FeedbackPhoto> { entry.Photo });
                    }
                    catch (RepairkitException e) when (e.Failure == RepairkitFailure.Daemon && e.Message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // photos are write-once per name: an earlier attempt got through before its reply was lost
                    }
                }

                return Outcome.Done;
            }
            catch (RepairkitException e)
            {
                error = e.Message;
                return e.IsConnectionFailure ? Outcome.Unreachable : Outcome.Failed;
            }
        }
    }
}
