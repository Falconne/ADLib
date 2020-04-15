using ADLib.Logging;
using ADLib.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ADLib.Exceptions;

namespace ADLib.Git
{
    public class Repo
    {
        public string Root { get; set; }

        public string Url { get; set; }

        public string DefaultRemote = "origin";

        public Repo(string root, string url)
        {
            Root = root;
            Url = url;

            if (string.IsNullOrEmpty(root) && string.IsNullOrEmpty(url))
                throw new ConfigurationException("Local directory or URL must be set");
        }

        [Obsolete("Use a MedallionShell run method instead")]
        public string Run(string arguments)
        {
            var previousDirectory = ChangeToRepoRoot();

            string result;
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

        public string RunAndFailIfNotExitZero(params string[] args)
        {
            var (result, output) = Run(args);
            if (result != 0)
            {
                throw new ConfigurationException("git command failed");
            }

            return output;
        }

        public string ChangeToRepoRoot()
        {
            var previousDirectory = Directory.GetCurrentDirectory();
            if (Directory.Exists(Root))
            {
                Directory.SetCurrentDirectory(Root);
            }

            return previousDirectory;
        }

        private (int ExitCode, string Output) Run(params string[] args)
        {
            var previousDirectory = ChangeToRepoRoot();

            try
            {
                return RunWithoutChangingRoot(args);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(previousDirectory))
                    Directory.SetCurrentDirectory(previousDirectory);
            }
        }

        private static (int ExitCode, string Output) RunWithoutChangingRoot(params string[] args)
        {
            // TODO use git object
            return Shell.Run("git.exe", args);
        }

        [Obsolete("Use a MedallionShell run method instead")]
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

            GenLog.Info($"Running: git.exe {arguments}");
            GenLog.Info($"\tFrom {Directory.GetCurrentDirectory()}");
            try
            {
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                p.WaitForExit();
            }
            catch (Exception e)
            {
                GenLog.Info(output.ToString());
                GenLog.Info($"Failed Command: git.exe {arguments}");
                throw new Exception("Failure: " + e.Message);
            }

            var result = output.ToString();

            if (p.ExitCode == 0)
            {
                return result;
            }
            GenLog.Info(result);
            GenLog.Info($"Failed Command: git.exe {arguments}");
            throw new Exception($"Command returned exit code: {p.ExitCode}");

        }

        public void MakeFreshClone()
        {
            Retry.OnException(
                () =>
                {
                    DeleteClone();
                    RunWithoutChangingRoot("clone", Url, Root);
                },
                $"Cloning fresh repo from {Url} to {Root}");
        }

        public void DeleteClone()
        {
            FileSystem.DeleteDirectory(Root);
        }

        public bool IsClean()
        {
            var result = RunAndFailIfNotExitZero("status",  "--porcelain",  "--untracked-files=no");
            return string.IsNullOrWhiteSpace(result);
        }

        public string GetName()
        {
            if (Url.ToLower().StartsWith("http"))
                return GetNameOfHttpRepo();

            var leaf = Url.Split(':').Last();
            return leaf.Replace(".git", "");

        }

        private string GetNameOfHttpRepo()
        {
            return Regex.Replace(Url, @"^.+?//.+?/", "");
        }

        public void StageModified()
        {
            GenLog.Info("Staging modified files");
            RunAndFailIfNotExitZero("add -u");
            RunAndFailIfNotExitZero("status");
        }

        public void Commit(string message)
        {
            GenLog.Info("Committing staged files");
            if (message.Contains('\n'))
            {
                throw new Exception("Multi-line messages are not supported by this method");
            }

            var result = RunAndFailIfNotExitZero("commit", message);
            GenLog.Info(result);
        }

        public void PushWithRebase()
        {
            Retry.OnException(() => RunAndFailIfNotExitZero("pull", "rebase"), "Pulling before push...");
            Retry.OnException(() => RunAndFailIfNotExitZero("push"), "Pushing...");
        }

        public void Fetch()
        {
            Retry.OnException(() => RunAndFailIfNotExitZero("fetch"), "Fetching");
        }

        public IEnumerable<string> GetRemoteBranchList()
        {
            var output = RunAndFailIfNotExitZero("for-each-ref", "--format=%(refname:short)", $"refs/remotes/{DefaultRemote}");
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
            var output = RunAndFailIfNotExitZero("for-each-ref", "--format=%(refname:short)", $"refs/heads/{localName}");
            return !string.IsNullOrWhiteSpace(output);
        }

        public void Checkout(string localBranchName)
        {
            if (IsLocalBranch(localBranchName))
            {
                GenLog.Info($"Checking out local branch {localBranchName}");
                RunAndFailIfNotExitZero("checkout", localBranchName);
            }
            else
            {
                Fetch();
                var remoteBranchDesignation = $"{DefaultRemote}/{localBranchName}";
                GenLog.Info($"Checking out remote branch {remoteBranchDesignation} into local");
                RunAndFailIfNotExitZero("checkout", "-t", remoteBranchDesignation);
            }

            RunAndFailIfNotExitZero("submodule", "update", "--recursive");
        }
    }
}
