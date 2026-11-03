using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Schema.Unity.Editor.Feedback.Tests
{
    /// <summary>
    /// The socket path is made usable without anyone pasting anything: a leading <c>~</c> is expanded (a shell
    /// does that, the socket API does not), and a daemon sitting at the standard path is filled in. EditorPrefs
    /// is never touched: the stored value is handed in, the way <c>LoadFromEditorPrefs</c> hands it in.
    /// </summary>
    public class FeedbackSettingsTests
    {
        private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // ---- expanding ~ --------------------------------------------------------------------------------

        [Test]
        public void A_leading_tilde_becomes_the_home_directory()
        {
            var expanded = FeedbackSettings.ExpandUserPath("~/.schema-unity/repairkit/repairkit.sock");

            Assert.That(expanded, Does.Not.Contain("~"), "nothing a socket cannot be opened at is left behind");
            Assert.That(expanded, Is.EqualTo(Path.Combine(Home, ".schema-unity", "repairkit", "repairkit.sock")));
        }

        [Test]
        public void A_pasted_path_is_trimmed_before_it_is_expanded()
        {
            Assert.That(FeedbackSettings.ExpandUserPath("  ~/rk.sock\t"), Is.EqualTo(Path.Combine(Home, "rk.sock")));
        }

        [Test]
        public void A_bare_tilde_is_the_home_directory()
        {
            Assert.That(FeedbackSettings.ExpandUserPath("~"), Is.EqualTo(Home));
        }

        [TestCase("/tmp/sk-1234.sock")]      // an absolute path
        [TestCase("relative/rk.sock")]       // a relative path
        [TestCase("~other/rk.sock")]         // another account's home: not ours to guess at
        [TestCase("/tmp/~/rk.sock")]         // a ~ anywhere but the front is part of the name
        public void Every_other_path_is_handed_over_unchanged(string path)
        {
            Assert.That(FeedbackSettings.ExpandUserPath(path), Is.EqualTo(path));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Nothing_stays_nothing_so_feedback_stays_off(string path)
        {
            var expanded = FeedbackSettings.ExpandUserPath(path);

            Assert.That(expanded, Is.Empty);
            Assert.That(new FeedbackSettings { SocketPath = expanded }.IsConfigured, Is.False);
        }

        [Test]
        public void The_client_connects_to_the_expanded_path_not_to_the_tilde()
        {
            // Nothing is listening at this name, so the failure message names the path that was really tried.
            var name = "sk-missing-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".sock";

            var failure = Assert.Throws<RepairkitException>(() => new RepairkitClient("~/" + name, 500).Ping());

            Assert.That(failure.Failure, Is.EqualTo(RepairkitFailure.Unreachable));
            Assert.That(failure.Message, Does.Contain(Path.Combine(Home, name)));
            Assert.That(failure.Message, Does.Not.Contain("~/" + name), "the literal ~ path was never opened");
        }

        [Test]
        public void A_tilde_path_reaches_a_daemon_that_a_plain_path_reaches()
        {
            // In the home directory, so that typing "~/<name>" names this very socket. Dispose deletes it.
            var name = "sk-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".sock";
            using (new FakeRepairkitServer(Path.Combine(Home, name)))
            {
                Assert.That(new RepairkitClient("~/" + name, 3000).Ping(), Is.Not.Null, "the daemon answered a ~ path");
            }
        }

        // ---- the standard socket, filled in -------------------------------------------------------------

        [Test]
        public void The_standard_socket_is_the_one_the_daemon_doc_names()
        {
            Assert.That(FeedbackSettings.DefaultSocketPath, Does.StartWith(Home));
            Assert.That(FeedbackSettings.DefaultSocketPath.Replace('\\', '/'),
                Does.EndWith(".schema-unity/repairkit/repairkit.sock"));
        }

        [Test]
        public void With_nothing_stored_the_standard_socket_is_filled_in_when_it_is_there()
        {
            var probed = new List<string>();

            var resolved = FeedbackSettings.ResolveSocketPath(null, path => { probed.Add(path); return true; });

            Assert.That(resolved, Is.EqualTo(FeedbackSettings.DefaultSocketPath));
            Assert.That(new FeedbackSettings { SocketPath = resolved }.IsConfigured, Is.True, "Send works with no pasting");
            Assert.That(probed, Is.EqualTo(new[] { FeedbackSettings.DefaultSocketPath }));
        }

        [Test]
        public void With_nothing_stored_and_no_socket_there_feedback_is_still_unset()
        {
            var resolved = FeedbackSettings.ResolveSocketPath(null, _ => false);

            Assert.That(resolved, Is.Empty);
            Assert.That(new FeedbackSettings { SocketPath = resolved }.IsConfigured, Is.False);
        }

        [Test]
        public void A_stored_path_wins_over_the_standard_one_and_is_expanded_too()
        {
            var resolved = FeedbackSettings.ResolveSocketPath("~/elsewhere/rk.sock", _ => true);

            Assert.That(resolved, Is.EqualTo(Path.Combine(Home, "elsewhere", "rk.sock")));
        }

        [Test]
        public void A_stored_empty_path_is_off_by_hand_and_is_not_filled_in_again()
        {
            Assert.That(FeedbackSettings.ResolveSocketPath(string.Empty, _ => true), Is.Empty,
                "clearing the field switches feedback off, and it stays off");
        }

        [Test]
        public void A_live_unix_socket_counts_as_there()
        {
            // What the fill-in rests on: to the filesystem a bound Unix socket is a file, so File.Exists sees it.
            using (var server = new FakeRepairkitServer())
            {
                Assert.That(FeedbackSettings.SocketFileExists(server.SocketPath), Is.True);
            }

            Assert.That(FeedbackSettings.SocketFileExists("/tmp/sk-not-here-" + Guid.NewGuid().ToString("N") + ".sock"), Is.False);
            Assert.That(FeedbackSettings.SocketFileExists(null), Is.False);
        }
    }
}
