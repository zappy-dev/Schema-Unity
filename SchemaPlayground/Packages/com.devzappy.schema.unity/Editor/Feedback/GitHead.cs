using System;
using System.IO;

namespace Schema.Unity.Editor.Feedback
{
    /// <summary>
    /// Finds the commit a checkout is on by reading <c>.git</c> files, without running git. Used when the
    /// package is a <c>file:</c> reference into a working tree (PackageManager gives a commit only for a git
    /// URL dependency). Handles a plain checkout, a linked worktree (<c>.git</c> is a file naming its git
    /// directory and a <c>commondir</c>), a detached HEAD, and branches that live in <c>packed-refs</c>.
    /// </summary>
    internal static class GitHead
    {
        /// <summary>The 40-hex commit HEAD points at, or null if <paramref name="startDirectory"/> is not inside a checkout.</summary>
        public static string TryRead(string startDirectory)
        {
            try
            {
                var directory = string.IsNullOrEmpty(startDirectory) ? null : new DirectoryInfo(startDirectory);
                while (directory != null)
                {
                    var dotGit = Path.Combine(directory.FullName, ".git");
                    if (Directory.Exists(dotGit)) return ReadHead(dotGit, dotGit);
                    if (File.Exists(dotGit))
                    {
                        var gitDir = ResolveGitFile(dotGit, directory.FullName);
                        return gitDir == null ? null : ReadHead(gitDir, CommonDir(gitDir));
                    }

                    directory = directory.Parent;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                // an unreadable .git is "no commit", not a failure to file feedback
            }

            return null;
        }

        private static string ResolveGitFile(string dotGitFile, string workTree)
        {
            foreach (var line in File.ReadAllLines(dotGitFile))
            {
                if (!line.StartsWith("gitdir:", StringComparison.Ordinal)) continue;
                var path = line.Substring("gitdir:".Length).Trim();
                return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(workTree, path));
            }

            return null;
        }

        private static string CommonDir(string gitDir)
        {
            var file = Path.Combine(gitDir, "commondir");
            if (!File.Exists(file)) return gitDir;
            var relative = File.ReadAllText(file).Trim();
            return Path.GetFullPath(Path.IsPathRooted(relative) ? relative : Path.Combine(gitDir, relative));
        }

        private static string ReadHead(string gitDir, string commonDir)
        {
            var headFile = Path.Combine(gitDir, "HEAD");
            if (!File.Exists(headFile)) return null;

            var head = File.ReadAllText(headFile).Trim();
            if (!head.StartsWith("ref:", StringComparison.Ordinal)) return IsCommit(head) ? head : null;

            var reference = head.Substring("ref:".Length).Trim();
            foreach (var dir in new[] { gitDir, commonDir })
            {
                var refFile = Path.Combine(dir, reference);
                if (!File.Exists(refFile)) continue;
                var value = File.ReadAllText(refFile).Trim();
                if (IsCommit(value)) return value;
            }

            var packed = Path.Combine(commonDir, "packed-refs");
            if (!File.Exists(packed)) return null;
            foreach (var line in File.ReadAllLines(packed))
            {
                if (line.Length == 0 || line[0] == '#' || line[0] == '^') continue;
                var space = line.IndexOf(' ');
                if (space != 40) continue;
                if (string.Equals(line.Substring(space + 1).Trim(), reference, StringComparison.Ordinal) && IsCommit(line.Substring(0, 40)))
                {
                    return line.Substring(0, 40);
                }
            }

            return null;
        }

        private static bool IsCommit(string value)
        {
            if (value == null || value.Length != 40) return false;
            foreach (var c in value)
            {
                var hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex) return false;
            }

            return true;
        }
    }
}
