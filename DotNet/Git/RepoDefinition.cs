using ADLib.Exceptions;
using ADLib.Util;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;


namespace ADLib.Git
{
    public class RepoDefinition
    {
        public string Url { get; set; }

        public string Name => GetNameFromUrl(Url);


        public RepoDefinition(string url)
        {
            Url = url;

            if (string.IsNullOrEmpty(url))
                throw new ConfigurationException("URL must be set");
        }

        public Repo CloneUnder(string directory, params string[] extraArgs)
        {
            var name = GetNameFromUrl(Url);
            var root = Path.Combine(directory, name);
            var args = new List<string>
            {
                "clone"
            };

            args.AddRange(extraArgs);
            args.AddRange(
                new[]
                {
                    Url,
                    root
                });

            void CloneSafely()
            {
                FileSystem.InitialiseDirectory(root);
                var (result, _, _) = RunWithoutChangingRoot(args.ToArray());
                if (result != 0)
                {
                    throw new ConfigurationException("Cloning failed");
                }
            }

            Retry.OnException(CloneSafely, $"Cloning {Url} to {directory}");

            var repo = new Repo(root, this);
            return repo;
        }

        private static (int ExitCode, string StdOut, string StdErr) RunWithoutChangingRoot(params string[] args)
        {
            return Client.Run(args);
        }

        // TODO Shouldn't be company specific
        public static string GetNameFromUrl(string url)
        {
            if (url.ToLowerInvariant().Contains("dev.azure.com"))
                return GetNameFromAzureUrl(url);

            if (url.ToLowerInvariant().Contains("git@akl-gitlab"))
                return GetNameFromGitlabSshUrl(url);

            if (url.ToLowerInvariant().Contains("akl-gitlab"))
                return GetNameFromGitlabUrl(url);

            throw new ConfigurationException($"Unrecognised server: {url}");
        }

        private static string GetNameFromAzureUrl(string url)
        {
            var suffix = Regex.Replace(url, @".+\.com/", "");
            return suffix.Replace("/_git", "");
        }

        private static string GetNameFromGitlabUrl(string url)
        {
            return Regex.Replace(url, @".+\.net/", "");
        }

        private static string GetNameFromGitlabSshUrl(string url)
        {
            var leaf = url.Split(':').Last();
            return leaf.Replace(".git", "");

        }
    }
}