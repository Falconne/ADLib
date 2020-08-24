using ADLib.Exceptions;
using ADLib.Logging;
using ADLib.Util;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


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

        public Repo CloneIfNotExistUnder(string directory, params string[] extraArgs)
        {
            return CloneIfNotExistUnderAsync(directory, extraArgs).Result;
        }

        public async Task<Repo> CloneIfNotExistUnderAsync(string directory, params string[] extraArgs)
        {
            var root = GetGeneratedRoot(directory);
            GenLog.Info($"Checking for existing clone at: {root}");
            var configPath = Path.Combine(root, ".git", "config");
            var bareConfigPath = Path.Combine(root, "config");

            if (!File.Exists(configPath) && !File.Exists(bareConfigPath))
                return await CloneUnder(directory, extraArgs);

            GenLog.Info("Clone found, not doing a new clone");
            var repo = new Repo(root, this);
            repo.Fetch();
            return repo;
        }

        public async Task<Repo> CloneUnder(string directory, params string[] extraArgs)
        {
            var root = GetGeneratedRoot(directory);
            var args = new List<string>
            {
                "clone",
                "-c",
                "core.longpaths=true"
            };

            args.AddRange(extraArgs);
            args.AddRange(
                new[]
                {
                    Url,
                    root
                });

            async Task CloneSafely()
            {
                FileSystem.InitialiseDirectory(root);
                var (result, _, _) = await RunWithoutChangingRoot(args.ToArray());
                if (result != 0)
                {
                    throw new ConfigurationException("Cloning failed");
                }
            }

            await Retry.OnExceptionAsync(CloneSafely, $"Cloning {Url} to {directory}");

            var repo = new Repo(root, this);
            return repo;
        }

        private string GetGeneratedRoot(string directory)
        {
            var name = GetNameFromUrl(Url);
            var root = Path.Combine(directory, name);
            return root;
        }

        private static async Task<(int ExitCode, string StdOut, string StdErr)> RunWithoutChangingRoot(params string[] args)
        {
            return await Client.RunAsync(args);
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
            return url.Split(':').Last().Split('/').Last();
        }
    }
}