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

        public Repo RunFluent(params string[] args)
        {
            RunAndFailIfNotExitZero(args);

            return this;
        }

        public string  RunAndGetOutput(params string[] args)
        {
            return RunAndFailIfNotExitZero(args).StdOut.Trim('\n', '\r', ' ');
        }

        public IList<string> RunAndGetList(params string[] args)
        {
            return RunAndGetOutput(args)
                .Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None)
                .Select(l => l.Trim('\n', '\r', ' '))
                .ToList();
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

        private string ChangeToRepoRoot()
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

        // Stage modified files and return true if anything to commit
        public bool StageModified()
        {
            GenLog.Info("Staging modified files");
            return !RunFluent("add", "-u")
                .RunFluent("status")
                .IsClean();
        }

        // Stage all files and return true if anything to commit
        public bool StageAll()
        {
            GenLog.Info("Staging all files");
            return !RunFluent("add", "-A", "*")
                .RunFluent("status")
                .IsClean();
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

        public void Push()
        {
            Retry.OnException(() => RunAndFailIfNotExitZero("pull"), "Pulling before push...");
            Retry.OnException(() => RunAndFailIfNotExitZero("push"), "Pushing...");
        }


        // Fetch with retry
        public Repo Fetch()
        {
            Retry.OnException(() => RunAndFailIfNotExitZero("fetch"), "Fetching");
            return this;
        }

        // Pull with retry
        public Repo Pull(params string[] args)
        {
            var newArgs = new List<string>
            {
                "pull"
            };

            newArgs.AddRange(args);

            Retry.OnException(() => RunAndFailIfNotExitZero(newArgs.ToArray()), "Pulling");
            return this;
        }

        public Repo ResetHard()
        {
            return RunFluent("reset", "--hard");
        }

        public Repo CleanUntracked()
        {
            return RunFluent("clean", "-ffxd");
        }

        public Repo ResetAndClean()
        {
            return ResetHard().CleanUntracked();
        }

        public Repo ResetToTrackingAndClean()
        {
            return RunFluent("reset", "--hard", "@{u}").CleanUntracked();
        }

        public IEnumerable<string> GetRemoteBranchList()
        {
            Fetch();
            var output = Shell.GetCombinedOutput(
                RunAndFailIfNotExitZero(
                    "for-each-ref", "--format=%(refname:short)", $"refs/remotes/{DefaultRemote}"));

            return output.Split('\n')
                .Where(b => !string.IsNullOrWhiteSpace(b) && !b.EndsWith("/HEAD"));
        }

        // Returns all remote branch refs without remote prefix
        public IEnumerable<string> GetLogicalBranchList()
        {
            GenLog.Info($"Gathering logical branch list in {Name}");
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

        public Repo CheckoutBranch(string localBranchName, bool updateSubmodules = false)
        {
            if (IsLocalBranch(localBranchName))
            {
                GenLog.Info($"Checking out local branch {localBranchName}");
                RunAndFailIfNotExitZero("-c", "core.longpaths=true", "checkout", localBranchName);
            }
            else
            {
                Fetch();
                var remoteBranchDesignation = $"{DefaultRemote}/{localBranchName}";
                GenLog.Info($"Checking out remote branch {remoteBranchDesignation} into local");
                RunAndFailIfNotExitZero("-c", "core.longpaths=true", "checkout", "-t", remoteBranchDesignation);
            }

            if (updateSubmodules)
                InitSubmodules();

            return this;
        }

        public Repo InitSubmodules(params string[] args)
        {
            var fullArgs = new List<string>
            {
                "-c", "core.longpaths=true", "submodule", "update", "--init", "--recursive"
            };

            fullArgs.AddRange(args);

            return RunFluent(fullArgs.ToArray());
        }

        public string GetCurrentBranchName()
        {
            return RunAndGetOutput("rev-parse", "--abbrev-ref", "HEAD");
        }
    }
}
