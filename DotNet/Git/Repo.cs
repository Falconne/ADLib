using ADLib.Exceptions;
using ADLib.Logging;
using ADLib.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ADLib.Git
{
    public class Repo
    {
        public string Root { get; set; }

        public string Url { get; set; }

        public string DefaultRemote = "origin";

        private string _name;


        public Repo(string root, string url)
        {
            Root = root;
            Url = url;

            if (string.IsNullOrEmpty(root) && string.IsNullOrEmpty(url))
                throw new ConfigurationException("Local directory or URL must be set");
        }

        public (string StdOut, string StdErr) RunAndFailIfNotExitZero(params string[] args)
        {
            var (result, stdout, stderr) = Run(args);
            if (result != 0)
            {
                throw new ConfigurationException("git command failed");
            }

            return (stdout, stderr);
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

        private (int ExitCode, string StdOut, string StdErr) Run(params string[] args)
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

        private static (int ExitCode, string StdOut, string StdErr) RunWithoutChangingRoot(params string[] args)
        {
            return Client.Run(args);
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
            var result= RunAndFailIfNotExitZero("status",  "--porcelain",  "--untracked-files=no");
            return string.IsNullOrWhiteSpace(Shell.GetCombinedOutput(result));
        }

        public string GetName()
        {
            if (!string.IsNullOrEmpty(_name))
                return _name;

            if (Url.ToLower().StartsWith("http"))
                throw new ConfigurationException("Repo name is not set");

            var leaf = Url.Split(':').Last();
            return leaf.Replace(".git", "");

        }

        public void SetName(string name)
        {
            _name = name;
        }

        public void StageModified()
        {
            GenLog.Info("Staging modified files");
            RunAndFailIfNotExitZero("add", "-u");
            RunAndFailIfNotExitZero("status");
        }

        public void Commit(string message)
        {
            GenLog.Info("Committing staged files");
            if (message.Contains('\n'))
            {
                throw new Exception("Multi-line messages are not supported by this method");
            }

            RunAndFailIfNotExitZero("commit", message);
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
            var output = Shell.GetCombinedOutput(
                RunAndFailIfNotExitZero(
                    "for-each-ref", "--format=%(refname:short)", $"refs/remotes/{DefaultRemote}"));

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
            var output = Shell.GetCombinedOutput(
                RunAndFailIfNotExitZero(
                    "for-each-ref", "--format=%(refname:short)", $"refs/heads/{localName}"));

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
