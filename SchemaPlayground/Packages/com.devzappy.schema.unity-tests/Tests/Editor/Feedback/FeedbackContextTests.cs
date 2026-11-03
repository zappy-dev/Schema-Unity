using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Schema.Unity.Editor.Feedback.Tests
{
    /// <summary>A1.2: the report's context names the host project, Unity version, package version and commit, and the open scheme.</summary>
    public class FeedbackContextTests
    {
        private string temp;

        [SetUp]
        public void SetUp() => temp = FeedbackTestKit.NewTempDir("ctx");

        [TearDown]
        public void TearDown() => FeedbackTestKit.DeleteQuietly(temp);

        private static FeedbackContextInputs SampleInputs() => new FeedbackContextInputs
        {
            ProjectPath = "/Users/someone/src/tank",
            ProjectName = "tank",
            UnityVersion = "6000.6.0f1",
            PackageVersion = "0.3.1",
            PackageCommit = "e9089e0a1b2c3d4e5f60718293a4b5c6d7e8f901",
            ManifestPath = "/Users/someone/src/tank/Content/Manifest.json",
            ContentPath = "/Users/someone/src/tank/Content",
            SelectedScheme = "Weapons",
            SelectedCell = new FeedbackCell { Attribute = "Tooltip", Row = 4, Value = "Coax MG" },
        };

        [Test]
        public void Context_names_the_host_project_unity_package_and_open_scheme()
        {
            var ctx = FeedbackContext.Build(SampleInputs());

            Assert.That((string)ctx["project_path"], Is.EqualTo("/Users/someone/src/tank"));
            Assert.That((string)ctx["project_name"], Is.EqualTo("tank"));
            Assert.That((string)ctx["unity_version"], Is.EqualTo("6000.6.0f1"));
            Assert.That((string)ctx["package_version"], Is.EqualTo("0.3.1"));
            Assert.That((string)ctx["package_commit"], Is.EqualTo("e9089e0a1b2c3d4e5f60718293a4b5c6d7e8f901"));
            Assert.That((string)ctx["manifest_path"], Does.EndWith("Content/Manifest.json"));
            Assert.That((string)ctx["content_path"], Does.EndWith("Content"));
            Assert.That((string)ctx["selected_scheme"], Is.EqualTo("Weapons"));
            Assert.That((string)ctx["selected_cell"]["attribute"], Is.EqualTo("Tooltip"));
            Assert.That((int)ctx["selected_cell"]["row"], Is.EqualTo(4));
            Assert.That((string)ctx["selected_cell"]["value"], Is.EqualTo("Coax MG"));
        }

        [Test]
        public void Context_carries_no_hold_mark_so_the_daemon_may_repair_it()
        {
            var ctx = FeedbackContext.Build(new FeedbackContextInputs());

            Assert.That(ctx["hold_for_review"], Is.Null);
        }

        [Test]
        public void Empty_values_are_left_out_rather_than_sent_as_blanks()
        {
            var ctx = FeedbackContext.Build(new FeedbackContextInputs { ProjectName = "tank" });

            Assert.That(ctx.ContainsKey("package_commit"), Is.False);
            Assert.That(ctx.ContainsKey("selected_scheme"), Is.False);
            Assert.That(ctx.ContainsKey("selected_cell"), Is.False);
            Assert.That(ctx.ContainsKey("scheme_json"), Is.False);
            Assert.That((string)ctx["project_name"], Is.EqualTo("tank"));
        }

        [Test]
        public void Scheme_data_travels_only_when_it_was_asked_for()
        {
            var without = SampleInputs();
            Assert.That(FeedbackContext.Build(without).ContainsKey("scheme_json"), Is.False);

            var with = SampleInputs();
            with.SchemeJson = "{\"name\":\"Weapons\"}";
            Assert.That((string)FeedbackContext.Build(with)["scheme_json"], Is.EqualTo("{\"name\":\"Weapons\"}"));
        }

        [Test]
        public void Oversized_scheme_data_is_cut_and_says_so()
        {
            var inputs = SampleInputs();
            inputs.SchemeJson = new string('x', FeedbackContext.MaxSchemeJsonChars + 500);

            var ctx = FeedbackContext.Build(inputs);

            Assert.That(((string)ctx["scheme_json"]).Length, Is.LessThan(FeedbackContext.MaxSchemeJsonChars + 100));
            Assert.That((bool)ctx["scheme_json_truncated"], Is.True);
        }

        [Test]
        public void Console_lines_are_included_with_the_limit_of_where_they_come_from()
        {
            var inputs = SampleInputs();
            inputs.ConsoleLines.Add("Schema: loaded manifest");
            inputs.ConsoleLines.Add("[Error] Schema: could not save");

            var ctx = FeedbackContext.Build(inputs);

            Assert.That(ctx["console_lines"], Is.InstanceOf<JArray>());
            Assert.That(((JArray)ctx["console_lines"]).Count, Is.EqualTo(2));
            Assert.That((string)ctx["console_lines_note"], Does.Contain("domain reload"));
        }

        [Test]
        public void Console_tail_keeps_the_last_fifty_lines_that_mention_schema()
        {
            var tail = new ConsoleTail();
            for (var i = 0; i < 70; i++) tail.Add(LogType.Log, "Schema line " + i);
            for (var i = 0; i < 20; i++) tail.Add(LogType.Log, "something else " + i);
            tail.Add(LogType.Error, "SchemaEditorWindow blew up");

            var lines = tail.Snapshot();

            Assert.That(lines.Count, Is.EqualTo(ConsoleTail.Capacity));
            Assert.That(ConsoleTail.Capacity, Is.EqualTo(50));
            Assert.That(lines[lines.Count - 1], Does.Contain("blew up"));
            Assert.That(lines[lines.Count - 1], Does.StartWith("[Error]"));
            Assert.That(lines, Has.None.Contain("something else"));
            Assert.That(lines[0], Does.Contain("Schema line 21"), "the oldest 21 of 71 fell out");
        }

        [Test]
        public void Console_tail_cuts_very_long_lines()
        {
            var tail = new ConsoleTail();
            tail.Add(LogType.Log, "Schema " + new string('y', 5000));

            Assert.That(tail.Snapshot()[0].Length, Is.LessThanOrEqualTo(ConsoleTail.MaxLineChars + 16));
        }

        [Test]
        public void Capture_reads_the_live_editor_without_a_schema_window_open()
        {
            var inputs = FeedbackContextCapture.Capture(null, includeSchemeData: false);

            Assert.That(inputs.UnityVersion, Is.EqualTo(Application.unityVersion));
            Assert.That(inputs.ProjectPath, Is.EqualTo(Path.GetFullPath(Application.dataPath + "/..")));
            Assert.That(inputs.ProjectName, Is.Not.Empty);
            Assert.That(inputs.PackageVersion, Does.Match(@"^\d+\.\d+\.\d+"), "the version of this package");
            Assert.That(inputs.SchemeJson, Is.Null);
            if (inputs.PackageCommit == null)
            {
                Assert.Inconclusive("this package is not inside a git checkout here, so there is no commit to name");
            }

            Assert.That(inputs.PackageCommit, Does.Match("^[0-9a-f]{40}$"));
        }

        [Test]
        public void Head_commit_is_read_from_a_plain_checkout()
        {
            var repo = Path.Combine(temp, "repo");
            var pkg = Path.Combine(repo, "Packages", "pkg");
            Directory.CreateDirectory(pkg);
            Directory.CreateDirectory(Path.Combine(repo, ".git", "refs", "heads"));
            File.WriteAllText(Path.Combine(repo, ".git", "HEAD"), "ref: refs/heads/main\n");
            File.WriteAllText(Path.Combine(repo, ".git", "refs", "heads", "main"), "1111111111111111111111111111111111111111\n");

            Assert.That(GitHead.TryRead(pkg), Is.EqualTo("1111111111111111111111111111111111111111"));
        }

        [Test]
        public void Head_commit_is_read_when_the_branch_is_only_in_packed_refs()
        {
            var repo = Path.Combine(temp, "repo");
            Directory.CreateDirectory(Path.Combine(repo, ".git"));
            File.WriteAllText(Path.Combine(repo, ".git", "HEAD"), "ref: refs/heads/main\n");
            File.WriteAllText(Path.Combine(repo, ".git", "packed-refs"),
                "# pack-refs with: peeled fully-peeled sorted\n" +
                "2222222222222222222222222222222222222222 refs/heads/other\n" +
                "3333333333333333333333333333333333333333 refs/heads/main\n");

            Assert.That(GitHead.TryRead(repo), Is.EqualTo("3333333333333333333333333333333333333333"));
        }

        [Test]
        public void Head_commit_is_read_from_a_detached_head()
        {
            var repo = Path.Combine(temp, "repo");
            Directory.CreateDirectory(Path.Combine(repo, ".git"));
            File.WriteAllText(Path.Combine(repo, ".git", "HEAD"), "4444444444444444444444444444444444444444\n");

            Assert.That(GitHead.TryRead(repo), Is.EqualTo("4444444444444444444444444444444444444444"));
        }

        [Test]
        public void Head_commit_is_read_from_a_linked_worktree()
        {
            var main = Path.Combine(temp, "main");
            var linkedGitDir = Path.Combine(main, ".git", "worktrees", "wt");
            var worktree = Path.Combine(temp, "wt");
            Directory.CreateDirectory(linkedGitDir);
            Directory.CreateDirectory(Path.Combine(main, ".git", "refs", "heads"));
            Directory.CreateDirectory(Path.Combine(worktree, "Packages", "pkg"));
            File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: " + linkedGitDir + "\n");
            File.WriteAllText(Path.Combine(linkedGitDir, "HEAD"), "ref: refs/heads/feature\n");
            File.WriteAllText(Path.Combine(linkedGitDir, "commondir"), "../..\n");
            File.WriteAllText(Path.Combine(main, ".git", "refs", "heads", "feature"), "5555555555555555555555555555555555555555\n");

            Assert.That(GitHead.TryRead(Path.Combine(worktree, "Packages", "pkg")), Is.EqualTo("5555555555555555555555555555555555555555"));
        }

        [Test]
        public void No_git_means_no_commit_rather_than_an_error()
        {
            Assert.That(GitHead.TryRead(temp), Is.Null);
        }
    }
}
