using ADLib.Exceptions;
using ADLib.Logging;
using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ADLib.Util
{
    public static class Shell
    {
        public static int RunAndGetExitCode(string program, params object[] args)
        {
            GenLog.Info($"{program} {string.Join(" ", args)}");
            var command = Command.Run(program, args)
                .RedirectTo(Console.Out)
                .RedirectStandardErrorTo(Console.Error);

            command.Wait();
            return command.Result.ExitCode;
        }

        public static void RunAndDetach(string program, params object[] args)
        {
            Command.Run(program, args);
        }

        public static (int exitCode, string stdout, string stderr) Run(
            string program, params object[] args)
        {
            return RunAsync(program, args).Result;
        }

        public static async Task<(int exitCode, string stdout, string stderr)> RunAsync(
            string program, params object[] args)
        {
            var (exitCode, stdout, stderr) = await RunSilent(program, args);

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                GenLog.Info("=====================================stdout=====================================");
                GenLog.Info(stdout);
            }
            else
            {
                GenLog.Info("<no stdout>");
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                GenLog.Info("=====================================stderr=====================================");
                GenLog.Info(stderr);
            }

            GenLog.Info("================================================================================");

            return (exitCode, stdout, stderr);
        }

        public static async Task<(int exitCode, string stdout, string stderr)> RunSilent(string program, params object[] args)
        {
            var argsPrinted = args.Length == 0 ? "" : string.Join(" ", args);
            GenLog.Debug($"{program} {argsPrinted}");

            var command = Command.Run(program, args);

            await command.Task;
            var stdout = await command.StandardOutput.ReadToEndAsync();
            var stderr = await command.StandardError.ReadToEndAsync();

            return (command.Result.ExitCode, stdout, stderr);
        }

        public static void RunAndFailIfNotExitZero(string program, params object[] args)
        {
            var exitCode = RunAndGetExitCode(program, args);
            if (exitCode != 0)
            {
                throw new ConfigurationException($"Exit code was {exitCode}");
            }
        }

        public static string GetScriptDir()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetFileInScriptDir(string filename)
        {
            var file = Path.Combine(GetScriptDir(), filename);
            if (!File.Exists(file))
            {
                throw new ConfigurationException($"File not found {file}");
            }

            return file;
        }

        public static string GetCurrentScript()
        {
            return System.Diagnostics.Process.GetCurrentProcess()?.MainModule?.FileName;
        }

        // Searches local dir PATH environment for command 'name' and returns absolute path to same
        // if found, otherwise null
        public static string GetExecutableInPath(string name)
        {
            if (!string.IsNullOrWhiteSpace(Path.GetDirectoryName(name)))
            {
                if (File.Exists(name))
                    return name;

                throw new ConfigurationException(
                    $"Argument to GetExecutableInPath should be a filename. '{name}' does not exist.");
            }

            if (string.IsNullOrWhiteSpace(Path.GetExtension(name)))
            {
                // Iterate over platform specific extensions; cmd, bat, ps1, sh, <none>
                name += ".exe";
            }

            GenLog.Info($"Looking for command {name}");

            string DoSearch()
            {
                if (File.Exists(name))
                    return Path.Combine(Directory.GetCurrentDirectory(), name);

                var fileInScriptDir = Path.Combine(GetScriptDir(), name);
                if (File.Exists(fileInScriptDir))
                    return fileInScriptDir;

                var dirsInPath = Environment.GetEnvironmentVariable("PATH")?.Split(';');
                if (dirsInPath == null || dirsInPath.Length == 0)
                {
                    GenLog.Warning("PATH environment variable is empty");
                    return null;
                }

                foreach (var dir in dirsInPath)
                {
                    var fileInDir = Path.Combine(dir, name);
                    if (File.Exists(fileInDir))
                        return fileInDir;
                }

                return null;
            }

            var result = DoSearch();
            if (string.IsNullOrWhiteSpace(result))
                return null;

            GenLog.Info($"Found at {result}");
            return result;
        }

        public static int RunPowerShellScriptAndGetExitCode(string script, params object[] args)
        {
            script = GetExecutableInPath(script);
            if (string.IsNullOrWhiteSpace(script))
            {
                throw new ConfigurationException($"Script not found {script}");
            }

            var command = new List<object>
            {
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                script
            };

            if (args.Length > 0)
                command.AddRange(args);

            return RunAndGetExitCode("powershell.exe", command.ToArray());
        }

        public static void RunPowerShellScriptAndFailIfNotExitZero(string script, params object[] args)
        {
            if (RunPowerShellScriptAndGetExitCode(script, args) != 0)
            {
                throw new ConfigurationException($"Command failed: {script}");
            }
        }

        // True if environment variable is set to a generic "not false" value
        public static bool IsEnvironmentVariableTrue(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            GenLog.Info($"Env: {name} => {value}");
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (value.ToLowerInvariant() == "false")
                return false;

            if (int.TryParse(value, out var result))
            {
                if (result == 0)
                    return false;
            }

            return true;
        }

        public static string GetCombinedOutput(ValueTuple<string, string> tuple)
        {
            var result = "";
            if (!string.IsNullOrWhiteSpace(tuple.Item1))
            {
                result += $"{tuple.Item1}\n";
            }

            if (!string.IsNullOrWhiteSpace(tuple.Item2))
            {
                result += $"{tuple.Item2}";
            }

            return result;
        }
    }
}