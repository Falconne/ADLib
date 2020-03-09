using ADLib.Exceptions;
using ADLib.Logging;
using Medallion.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ADLib.Util
{
    public static class Shell
    {
        // Copied from https://stackoverflow.com/questions/15365038/how-processstartinfo-argument-consider-arguments
        [Obsolete("Use MedallionShell commands")]
        public static string EscapeArgument(string argument)
        {
            using (var characterEnumerator = argument.GetEnumerator())
            {
                var escapedArgument = new StringBuilder();
                var backslashCount = 0;
                var needsQuotes = false;

                while (characterEnumerator.MoveNext())
                {
                    switch (characterEnumerator.Current)
                    {
                        case '\\':
                            // Backslashes are simply passed through, except when they need
                            // to be escaped when followed by a \", e.g. the argument string
                            // \", which would be encoded to \\\"
                            backslashCount++;
                            escapedArgument.Append('\\');
                            break;

                        case '\"':
                            // Escape any preceding backslashes
                            for (var c = 0; c < backslashCount; c++)
                            {
                                escapedArgument.Append('\\');
                            }

                            // Append an escaped double quote.
                            escapedArgument.Append("\\\"");

                            // Reset the backslash counter.
                            backslashCount = 0;
                            break;

                        case ' ':
                        case '\t':
                            // White spaces are escaped by surrounding the entire string with
                            // double quotes, which should be done at the end to prevent
                            // multiple wrappings.
                            needsQuotes = true;

                            // Append the whitespace
                            escapedArgument.Append(characterEnumerator.Current);

                            // Reset the backslash counter.
                            backslashCount = 0;
                            break;

                        default:
                            // Reset the backslash counter.
                            backslashCount = 0;

                            // Append the current character
                            escapedArgument.Append(characterEnumerator.Current);
                            break;
                    }
                }

                // No need to wrap in quotes
                if (!needsQuotes)
                {
                    return escapedArgument.ToString();
                }

                // Prepend the "
                escapedArgument.Insert(0, '"');

                // Escape any preceding backslashes before appending the "
                for (var c = 0; c < backslashCount; c++)
                {
                    escapedArgument.Append('\\');
                }

                // Append the final "
                escapedArgument.Append('\"');

                return escapedArgument.ToString();
            }
        }

        // Copied from https://stackoverflow.com/questions/15365038/how-processstartinfo-argument-consider-arguments
        [Obsolete("Use MedallionShell commands")]
        public static string EscapeArguments(params string[] args)
        {
            var argEnumerator = args.GetEnumerator();
            var arguments = new StringBuilder();

            if (!argEnumerator.MoveNext())
            {
                return string.Empty;
            }

            arguments.Append(EscapeArgument((string)argEnumerator.Current));

            while (argEnumerator.MoveNext())
            {
                arguments.Append(' ');
                arguments.Append(EscapeArgument((string)argEnumerator.Current));
            }

            return arguments.ToString();
        }

        [Obsolete("Use MedallionShell commands")]
        public static int RunAndGetExitCode(string program, string argumentString)
        {
            var p = Process.Start(program, argumentString);
            p.WaitForExit();
            return p.ExitCode;
        }

        [Obsolete("Use MedallionShell commands")]
        public static int RunAndGetExitCode(string program, params object[] args)
        {
            return RunAndGetExitCodeMS(program, args);
        }

        [Obsolete("Use MedallionShell commands")]
        public static void RunAndFailIfNotExitZero(string program, string argumentString)
        {
            var exitCode = RunAndGetExitCode(program, argumentString);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Command: '{program} {argumentString}' returned {exitCode}");
            }
        }

        public static int RunAndGetExitCodeMS(string program, params object[] args)
        {
            GenLog.Info($"{program} {string.Join(" ", args)}");
            var command = Command.Run(program, args)
                .RedirectTo(Console.Out)
                .RedirectStandardErrorTo(Console.Error);

            command.Wait();
            return command.Result.ExitCode;
        }

        public static (int exitCode, string output) Run(string program, params object[] args)
        {
            GenLog.Info($"{program} {string.Join(" ", args)}");
            var output = new StringWriter();
            var command = Command.Run(program, args)
                .RedirectTo(output)
                .RedirectStandardErrorTo(output);

            command.Wait();
            return (command.Result.ExitCode, output.ToString());
        }

        public static void RunAndFailIfNotExitZeroMS(string program, params object[] args)
        {
            var exitCode = RunAndGetExitCodeMS(program, args);
            if (exitCode != 0)
            {
                throw new ConfigurationException("");
            }
        }

        public static string GetScriptDir()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
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

            return RunAndGetExitCodeMS("powershell.exe", command.ToArray());
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
    }
}