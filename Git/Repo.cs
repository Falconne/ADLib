using ResourceResolver;
using STLogger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Git
{
    public class Repo
    {
        public readonly string Root;

        private readonly string _url;

        public string DefaultRemote = "origin";

        public Repo(string root, string url)
        {
            Root = root;
            _url = url;
        }

        public string Run(string arguments)
        {
            string previousDirectory = null;
            string result;

            if (Directory.Exists(Root))
            {
                previousDirectory = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(Root);
            }

            try
            {
                result = RunWithoutChangingRoot(arguments);

            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previousDirectory))
                    Directory.SetCurrentDirectory(previousDirectory);
            }

            return result;
        }

        public void RunAndShowOutput(string arguments)
        {
            LogWrapper.Info(Run(arguments));
        }

        private static string RunWithoutChangingRoot(string arguments)
        {
            var output = new StringBuilder();

            // FIXME check that git is in the PATH
            var p = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = "git",
                    Arguments = arguments
                }
            };

            var outputLock = new object();
            p.OutputDataReceived += (sender, args) => { lock (outputLock) { output.Append(args.Data + "\n"); } };
            p.ErrorDataReceived += (sender, args) => { lock (outputLock) { output.Append(args.Data + "\n"); } };

            LogWrapper.Info($"Running: git.exe {arguments}");
            LogWrapper.Info($"\tFrom {Directory.GetCurrentDirectory()}");
            try
            {
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                p.WaitForExit();
            }
            catch (Exception e)
            {
                LogWrapper.Info(output.ToString());
                LogWrapper.Info($"Failed Command: git.exe {arguments}");
                throw new Exception("Failure: " + e.Message);
            }

            var result = output.ToString();

            if (p.ExitCode == 0)
            {
                return result;
            }
            LogWrapper.Info(result);
            LogWrapper.Info($"Failed Command: git.exe {arguments}");
            throw new Exception($"Command returned exit code: {p.ExitCode}");

        }

        public void MakeFreshClone()
        {
            Util.Retry.OnException(
                () =>
                {
                    DeleteClone();
                    RunWithoutChangingRoot($"clone {_url} \"{Root}\"");
                },
                $"Cloning fresh repo from {_url} to {Root}");
        }

        public void DeleteClone()
        {
            Util.Path.DeleteDirectory(Root);
        }

        public bool IsClean()
        {
            var result = Run("status --porcelain --untracked-files=no");
            return string.IsNullOrWhiteSpace(result);
        }

        public string GetName()
        {
            // TODO handle this
            if (_url.ToLower().StartsWith("http"))
                return _url;

            var leaf = _url.Split(':').Last();
            return leaf.Replace(".git", "");

        }

        public void StageModified()
        {
            LogWrapper.Info("Staging modified files");
            Run("add -u");
            LogWrapper.Info(Run("status"));
        }

        public void Commit(string message)
        {
            LogWrapper.Info("Committing staged files");
            if (message.Contains('\n'))
            {
                throw new Exception("Multi-line messages are not supported by this method");
            }

            var messageConstruct = Util.Shell.EscapeArguments(args);

            var result = Run($"commit {messageConstruct}");
            LogWrapper.Info(result);
        }

        public void PushWithRebase()
        {
            Util.Retry.OnException(() => Run("pull --rebase"), "Pulling before push...");
            Util.Retry.OnException(() => Run("push"), "Pushing...");
        }

        public void Fetch()
        {
            Util.Retry.OnException(() => Run("fetch"), "Fetching");
        }

        public IEnumerable<string> GetRemoteBranchList()
        {
            var output = Run($"for-each-ref --format=%(refname:short) refs/remotes/{DefaultRemote}");
            return output.Split('\n')
                .Where(b => !string.IsNullOrWhiteSpace(b) && !b.EndsWith("/HEAD"));
        }

        // Returns all remote branch refs without remote prefix
        public IEnumerable<string> GetLogicalBranchList()
        {
            var cutLength = $"{DefaultRemote}/".Length;
            return GetRemoteBranchList()
                .Select(b => b.Substring(cutLength));
        }

        public bool IsLocalBranch(string localName)
        {
            var output = Run($"for-each-ref --format=%(refname:short) refs/heads/{localName}");
            return !string.IsNullOrWhiteSpace(output);
        }

        public void Checkout(string localBranchName)
        {
            if (IsLocalBranch(localBranchName))
            {
                LogWrapper.Info($"Checking out local branch {localBranchName}");
                RunAndShowOutput($"checkout {localBranchName}");
            }
            else
            {
                Fetch();
                var remoteBranchDesignation = $"{DefaultRemote}/{localBranchName}";
                LogWrapper.Info($"Checking out remote branch {remoteBranchDesignation} into local");
                RunAndShowOutput($"checkout -t {remoteBranchDesignation}");
            }

            RunAndShowOutput("submodule update --recursive");
        }
    }
}
