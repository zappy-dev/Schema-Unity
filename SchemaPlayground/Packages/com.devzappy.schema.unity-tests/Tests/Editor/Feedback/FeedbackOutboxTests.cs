using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Schema.Unity.Editor.Feedback.Tests
{
    /// <summary>A1.3: with the daemon stopped, a report survives in the outbox and is sent on the next connect.</summary>
    public class FeedbackOutboxTests
    {
        private string temp;
        private string outbox;
        private FakeRepairkitServer server;

        [SetUp]
        public void SetUp()
        {
            temp = FeedbackTestKit.NewTempDir("outbox");
            outbox = Path.Combine(temp, "outbox");
        }

        [TearDown]
        public void TearDown()
        {
            server?.Dispose();
            server = null;
            FeedbackTestKit.DeleteQuietly(temp);
        }

        private static FeedbackDraft Draft(string message) => new FeedbackDraft
        {
            Message = message,
            Context = FeedbackContext.Build(new FeedbackContextInputs { ProjectName = "tank" }),
            Photo = FeedbackTestKit.PngPhoto(),
        };

        private FeedbackSender SenderFor(string socketPath) =>
            new FeedbackSender(new FeedbackSettings { SocketPath = socketPath, OutboxRoot = outbox, TimeoutMs = 3000 });

        [Test]
        public void With_the_daemon_stopped_the_report_and_its_image_are_kept_on_disk()
        {
            var socketPath = FakeRepairkitServer.NewSocketPath(); // nothing listens here

            var result = SenderFor(socketPath).Send(Draft("first"));

            Assert.That(result.Status, Is.EqualTo(SendStatus.Queued));
            Assert.That(result.Message, Does.Contain("outbox"));
            var entries = new FeedbackOutbox(outbox).Pending();
            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(entries[0].Directory, "report.json")), Is.True);
            Assert.That(Directory.GetFiles(entries[0].Directory, "schema-editor.*").Length, Is.EqualTo(1));
            Assert.That(entries[0].Message, Is.EqualTo("first"));
            Assert.That(entries[0].Source, Is.EqualTo("schema-editor"));
            Assert.That((string)entries[0].Context["hold_for_review"], Is.EqualTo("phase 1: report only"));
        }

        [Test]
        public void On_the_next_connect_queued_reports_are_sent_oldest_first_and_the_outbox_empties()
        {
            var socketPath = FakeRepairkitServer.NewSocketPath();
            var first = SenderFor(socketPath).Send(Draft("first"));
            var second = SenderFor(socketPath).Send(Draft("second"));
            Assert.That(first.Status, Is.EqualTo(SendStatus.Queued));
            Assert.That(second.Status, Is.EqualTo(SendStatus.Queued));
            Assert.That(new FeedbackOutbox(outbox).Pending().Count, Is.EqualTo(2));

            server = new FakeRepairkitServer(socketPath); // the daemon comes back
            var third = SenderFor(socketPath).Send(Draft("third"));

            Assert.That(third.Status, Is.EqualTo(SendStatus.Sent), third.Message);
            var messages = server.Requests("submit").Select(r => (string)r["message"]).ToArray();
            Assert.That(messages, Is.EqualTo(new[] { "first", "second", "third" }));
            Assert.That(server.Photos().Count, Is.EqualTo(3));
            Assert.That(new FeedbackOutbox(outbox).Pending(), Is.Empty);
        }

        [Test]
        public void Flushing_alone_on_editor_load_delivers_what_was_queued()
        {
            var socketPath = FakeRepairkitServer.NewSocketPath();
            SenderFor(socketPath).Send(Draft("queued while down"));

            server = new FakeRepairkitServer(socketPath);
            var delivered = SenderFor(socketPath).FlushOutbox();

            Assert.That(delivered, Is.EqualTo(1));
            Assert.That(server.Requests("submit").Count, Is.EqualTo(1));
            Assert.That(new FeedbackOutbox(outbox).Pending(), Is.Empty);
        }

        [Test]
        public void Flushing_with_nothing_queued_never_touches_the_socket()
        {
            // a socket path that cannot connect: if flush tried, it would have to report a failure
            var delivered = SenderFor(FakeRepairkitServer.NewSocketPath()).FlushOutbox();

            Assert.That(delivered, Is.EqualTo(0));
            Assert.That(Directory.Exists(outbox), Is.False);
        }

        [Test]
        public void A_failed_screenshot_attach_keeps_the_report_id_so_the_retry_does_not_file_a_second_report()
        {
            server = new FakeRepairkitServer { AttachFailuresRemaining = 1 };

            var result = SenderFor(server.SocketPath).Send(Draft("photo fails once"));

            Assert.That(result.Status, Is.EqualTo(SendStatus.Sent), "the report itself was filed");
            Assert.That(result.ReportId, Is.EqualTo("r-1"));
            Assert.That(result.Message, Does.Contain("screenshot"));
            var pending = new FeedbackOutbox(outbox).Pending();
            Assert.That(pending.Count, Is.EqualTo(1));
            Assert.That(pending[0].SubmittedId, Is.EqualTo("r-1"));

            var delivered = SenderFor(server.SocketPath).FlushOutbox();

            Assert.That(delivered, Is.EqualTo(1));
            Assert.That(server.Requests("submit").Count, Is.EqualTo(1), "never submitted twice");
            Assert.That(server.Photos().Single().ReportId, Is.EqualTo("r-1"));
            Assert.That(new FeedbackOutbox(outbox).Pending(), Is.Empty);
        }

        [Test]
        public void An_entry_the_daemon_rejects_stays_queued_with_the_reason()
        {
            server = new FakeRepairkitServer { AttachFailuresRemaining = 100 };
            SenderFor(server.SocketPath).Send(Draft("never attaches"));

            SenderFor(server.SocketPath).FlushOutbox();

            var pending = new FeedbackOutbox(outbox).Pending();
            Assert.That(pending.Count, Is.EqualTo(1), "no report is lost");
            Assert.That(pending[0].LastError, Does.Contain("photo store unavailable"));
            Assert.That(pending[0].Attempts, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void An_outbox_entry_round_trips_through_disk()
        {
            var store = new FeedbackOutbox(outbox);
            var draft = Draft("round trip");

            var entry = store.Enqueue(draft, "daemon down");

            var back = new FeedbackOutbox(outbox).Pending().Single();
            Assert.That(back.Directory, Is.EqualTo(entry.Directory));
            Assert.That(back.Message, Is.EqualTo("round trip"));
            Assert.That(back.Context.ToString(), Is.EqualTo(draft.Context.ToString()));
            Assert.That(back.LastError, Is.EqualTo("daemon down"));
            Assert.That(back.Photo.Bytes, Is.EqualTo(draft.Photo.Bytes));
            Assert.That(back.Photo.Mime, Is.EqualTo("image/png"));
            Assert.That(back.SubmittedId, Is.Null);
        }

        [Test]
        public void An_unreadable_outbox_entry_is_skipped_not_fatal()
        {
            var store = new FeedbackOutbox(outbox);
            store.Enqueue(Draft("good"), null);
            var broken = Path.Combine(outbox, "00000000T000000000-broken");
            Directory.CreateDirectory(broken);
            File.WriteAllText(Path.Combine(broken, "report.json"), "{ not json");

            var pending = store.Pending();

            Assert.That(pending.Select(p => p.Message), Is.EqualTo(new[] { "good" }));
        }
    }
}
