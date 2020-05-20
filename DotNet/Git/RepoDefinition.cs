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

        public static string GetNameFromUrl(string url)
        {
            string nameFromUrl;

            if (url.ToLowerInvariant().StartsWith("http"))
                nameFromUrl = GetNameFromHttpsUrl(url);
            else if (url.ToLowerInvariant().StartsWith("git@"))
                nameFromUrl = GetNameFromSshUrl(url);
            else
                throw new ConfigurationException($"Unrecognised protocol: {url}");

            return Regex.Replace(nameFromUrl, @"\.git$", "");
        }

        private static string GetNameFromHttpsUrl(string url)
        {
            var suffix = Regex.Replace(url, @".+?/", "");
            return suffix.Replace("/_git", "");
        }

        private static string GetNameFromSshUrl(string url)
        {
            return url.Split(':').Last();
        }
    }
}