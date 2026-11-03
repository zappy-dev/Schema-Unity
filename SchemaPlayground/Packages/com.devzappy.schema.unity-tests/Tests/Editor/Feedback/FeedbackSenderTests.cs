using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback.Tests
{
    /// <summary>
    /// A1.5 (no socket path: explain, send nothing, write nothing) and A1.6 (the submit request carries
    /// hold_for_review), plus the happy path against a fake daemon, including the maybe_auto_fix that asks
    /// the daemon to start a repair.
    /// </summary>
    public class FeedbackSenderTests
    {
        private string temp;
        private string outbox;
        private FakeRepairkitServer server;

        [SetUp]
        public void SetUp()
        {
            temp = FeedbackTestKit.NewTempDir("sender");
            outbox = Path.Combine(temp, "outbox");
        }

        [TearDown]
        public void TearDown()
        {
            server?.Dispose();
            server = null;
            FeedbackTestKit.DeleteQuietly(temp);
        }

        private static FeedbackDraft Draft(string message = "the Coax tooltip column is cut off", bool withPhoto = true) =>
            new FeedbackDraft
            {
                Message = message,
                Context = FeedbackContext.Build(new FeedbackContextInputs { ProjectName = "tank", SelectedScheme = "Weapons" }),
                Photo = withPhoto ? FeedbackTestKit.PngPhoto() : null,
            };

        private FeedbackSender SenderFor(string socketPath) =>
            new FeedbackSender(new FeedbackSettings { SocketPath = socketPath, OutboxRoot = outbox, TimeoutMs = 3000 });

        // ---- A1.5 ---------------------------------------------------------------------------------------

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void With_no_socket_path_nothing_is_sent_and_nothing_is_written(string socketPath)
        {
            var before = ProjectFiles();

            var result = SenderFor(socketPath).Send(Draft());

            Assert.That(result.Status, Is.EqualTo(SendStatus.NotConfigured));
            Assert.That(result.Message, Does.Contain("socket"), "tells the person what to set");
            Assert.That(Directory.Exists(outbox), Is.False, "no outbox entry is written either");
            Assert.That(ProjectFiles(), Is.EqualTo(before), "the host project's files do not change");
        }

        [Test]
        public void With_no_socket_path_flushing_the_outbox_does_nothing()
        {
            Assert.That(SenderFor("").FlushOutbox(), Is.EqualTo(0));
            Assert.That(Directory.Exists(outbox), Is.False);
        }

        [Test]
        public void The_default_outbox_lives_under_the_home_directory_not_the_project()
        {
            var root = FeedbackSettings.DefaultOutboxRoot;

            Assert.That(root, Does.Contain(".schema-unity"));
            Assert.That(root.Replace('\\', '/'), Does.EndWith(".schema-unity/feedback-outbox"));
            Assert.That(root, Does.Not.StartWith(Path.GetFullPath(Application.dataPath + "/..")));
        }

        private static string[] ProjectFiles()
        {
            var project = Path.GetFullPath(Application.dataPath + "/..");
            return new[] { "Assets", "ProjectSettings" }
                .SelectMany(folder => Directory.GetFiles(Path.Combine(project, folder), "*", SearchOption.AllDirectories))
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .Select(path => path + "|" + new FileInfo(path).Length)
                .ToArray();
        }

        // ---- A1.6 ---------------------------------------------------------------------------------------

        [Test]
        public void The_submit_request_carries_the_source_and_no_hold_mark()
        {
            server = new FakeRepairkitServer();

            var result = SenderFor(server.SocketPath).Send(Draft());

            Assert.That(result.Status, Is.EqualTo(SendStatus.Sent), result.Message);
            var submit = server.Requests("submit").Single();
            Assert.That(submit["context"]["hold_for_review"], Is.Null);
            Assert.That((string)submit["source"], Is.EqualTo("schema-editor"));
            Assert.That((string)submit["message"], Is.EqualTo("the Coax tooltip column is cut off"));
        }

        [Test]
        public void The_caller_s_context_is_sent_unchanged()
        {
            server = new FakeRepairkitServer();
            var draft = Draft();
            draft.Context = new JObject { ["project_name"] = "tank" };

            SenderFor(server.SocketPath).Send(draft);

            var submit = server.Requests("submit").Single();
            Assert.That(JToken.DeepEquals(submit["context"], draft.Context), Is.True, submit["context"].ToString());
        }

        // ---- happy path ----------------------------------------------------------------------------------

        [Test]
        public void A_report_is_submitted_and_its_screenshot_attached_to_the_new_id()
        {
            server = new FakeRepairkitServer();
            var draft = Draft();

            var result = SenderFor(server.SocketPath).Send(draft);

            Assert.That(result.Status, Is.EqualTo(SendStatus.Sent), result.Message);
            Assert.That(result.ReportId, Is.EqualTo("r-1"));
            Assert.That(server.Requests().Select(r => (string)r["op"]),
                Is.EqualTo(new[] { "submit", "attach_photos", "maybe_auto_fix" }));
            var photo = server.Photos().Single();
            Assert.That(photo.ReportId, Is.EqualTo("r-1"));
            Assert.That(photo.Name, Is.EqualTo("schema-editor.png"));
            Assert.That(photo.Mime, Is.EqualTo("image/png"));
            Assert.That(photo.Bytes, Is.EqualTo(draft.Photo.Bytes));
            Assert.That(Directory.Exists(outbox) && Directory.GetDirectories(outbox).Length > 0, Is.False, "nothing left queued");
        }

        [Test]
        public void A_report_without_a_screenshot_does_not_call_attach_photos()
        {
            server = new FakeRepairkitServer();

            var result = SenderFor(server.SocketPath).Send(Draft(withPhoto: false));

            Assert.That(result.Status, Is.EqualTo(SendStatus.Sent), result.Message);
            Assert.That(server.Requests("attach_photos"), Is.Empty);
        }

        [Test]
        public void A_blank_message_is_refused_before_anything_is_sent()
        {
            server = new FakeRepairkitServer();

            var result = SenderFor(server.SocketPath).Send(Draft(message: "   "));

            Assert.That(result.Status, Is.EqualTo(SendStatus.Invalid));
            Assert.That(server.Requests(), Is.Empty);
            Assert.That(Directory.Exists(outbox), Is.False);
        }

        // ---- maybe_auto_fix -----------------------------------------------------------------------------

        [Test]
        public void A_sent_report_asks_the_daemon_to_repair_it_after_submit_and_attach_photos()
        {
            server = new FakeRepairkitServer();

            var result = SenderFor(server.SocketPath).Send(Draft());

            Assert.That(result.Status, Is.EqualTo(SendStatus.Sent), result.Message);
            var ops = server.Requests().Select(r => (string)r["op"]).ToList();
            Assert.That(ops, Does.Contain("maybe_auto_fix"), "repairkit starts a repair only when it is asked to");
            Assert.That(ops.IndexOf("maybe_auto_fix"), Is.GreaterThan(ops.IndexOf("submit")), "after the report is filed");
            Assert.That(ops.IndexOf("maybe_auto_fix"), Is.GreaterThan(ops.IndexOf("attach_photos")), "and after its photo is in");
            var ask = server.Requests("maybe_auto_fix").Single();
            Assert.That((string)ask["id"], Is.EqualTo(result.ReportId), "the id the submit reply gave");
        }

        [Test]
        public void A_report_without_a_screenshot_still_asks_the_daemon_to_repair_it()
        {
            server = new FakeRepairkitServer();

            var result = SenderFor(server.SocketPath).Send(Draft(withPhoto: false));

            Assert.That(result.Status, Is.EqualTo(SendStatus.Sent), result.Message);
            Assert.That(server.Requests().Select(r => (string)r["op"]),
                Is.EqualTo(new[] { "submit", "maybe_auto_fix" }));
            Assert.That((string)server.Requests("maybe_auto_fix").Single()["id"], Is.EqualTo(result.ReportId));
        }

        [Test]
        public void Flushing_the_outbox_asks_the_daemon_to_repair_what_it_delivers()
        {
            var socketPath = FakeRepairkitServer.NewSocketPath(); // nothing listens yet
            Assert.That(SenderFor(socketPath).Send(Draft()).Status, Is.EqualTo(SendStatus.Queued));

            server = new FakeRepairkitServer(socketPath); // the daemon comes back
            var delivered = SenderFor(socketPath).FlushOutbox();

            Assert.That(delivered, Is.EqualTo(1));
            Assert.That(server.Requests().Select(r => (string)r["op"]),
                Is.EqualTo(new[] { "submit", "attach_photos", "maybe_auto_fix" }));
            Assert.That((string)server.Requests("maybe_auto_fix").Single()["id"], Is.EqualTo("r-1"),
                "the id the flushed entry was filed under");
        }

        [Test]
        public void A_refused_repair_request_leaves_the_report_sent_neither_requeued_nor_filed_twice()
        {
            // The daemon refuses the repair request; the sender logs a warning and leaves the report alone.
            server = new FakeRepairkitServer { FailAutoFix = true };

            var result = SenderFor(server.SocketPath).Send(Draft());

            Assert.That(result.Status, Is.EqualTo(SendStatus.Sent), "the report is filed, so the dialog says Sent");
            Assert.That(result.ReportId, Is.EqualTo("r-1"));
            Assert.That(Directory.Exists(outbox) && Directory.GetDirectories(outbox).Length > 0, Is.False,
                "nothing is queued: a retry would file the same report again");

            SenderFor(server.SocketPath).FlushOutbox();

            Assert.That(server.Requests("submit").Count, Is.EqualTo(1), "never submitted twice");
            Assert.That(server.Photos().Count, Is.EqualTo(1), "and its screenshot is attached once");
        }

        // ---- the client ----------------------------------------------------------------------------------

        [Test]
        public void Ping_reaches_a_daemon_that_is_up_and_explains_one_that_is_down()
        {
            server = new FakeRepairkitServer();
            var pong = new RepairkitClient(server.SocketPath, 3000).Ping();
            Assert.That((string)pong["status"], Is.EqualTo("ok"));

            var down = FakeRepairkitServer.NewSocketPath();
            var ex = Assert.Throws<RepairkitException>(() => new RepairkitClient(down, 1000).Ping());
            Assert.That(ex.IsConnectionFailure, Is.True);
        }

        [Test]
        public void A_path_too_long_for_a_unix_socket_is_reported_as_unreachable_not_thrown_raw()
        {
            var tooLong = "/tmp/" + new string('a', 200) + ".sock";

            var ex = Assert.Throws<RepairkitException>(() => new RepairkitClient(tooLong, 1000).Ping());

            Assert.That(ex.IsConnectionFailure, Is.True);
        }
    }
}
