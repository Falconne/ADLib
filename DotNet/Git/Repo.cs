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

        public readonly RepoDefinition Definition;

        public string Name => Definition.Name;

        public string DefaultRemote = "origin";


        public Repo(string root, RepoDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ConfigurationException("Cloned repo must have a root");

            Root = root;
            Definition = definition;
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
            else
            {
                throw new ConfigurationException(
                    $"Cannot switch to repo root {Root}; directory not found");
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

        public void DeleteClone()
        {
            FileSystem.Delete(Root);
        }

        public bool IsClean()
        {
            var result = RunAndFailIfNotExitZero(
                "status", "--porcelain", "--untracked-files=no");

            return string.IsNullOrWhiteSpace(Shell.GetCombinedOutput(result));
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
            Retry.OnException(() => RunAndFailIfNotExitZero("pull", "--rebase"), "Pulling before push...");
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

        public void Checkout(string localBranchName, bool updateSubmodules = false)
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

            if (updateSubmodules)
                RunAndFailIfNotExitZero("submodule", "update", "--recursive", "--init");
        }
    }
}
