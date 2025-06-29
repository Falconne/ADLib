using ADLib.Exceptions;
using ADLib.Logging;
using Medallion.Shell;
using System.Diagnostics;

namespace ADLib.Util;

public static class Shell
{
    public static int RunAndGetExitCode(string program, params object[] args)
    {
        GenLog.Debug($"{program} {string.Join(" ", args)}");
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
        string program,
        params object[] args)
    {
        return RunAsync(program, args).Result;
    }

    public static async Task<(int exitCode, string stdout, string stderr)> RunAsync(
        string program,
        params object[] args)
    {
        var (exitCode, stdout, stderr) = await RunSilent(program, args).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            GenLog.Debug("=====================================stdout=====================================");
            GenLog.Debug(stdout);
        }
        else
        {
            GenLog.Debug("<no stdout>");
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            GenLog.Debug("=====================================stderr=====================================");
            GenLog.Debug(stderr);
        }

        GenLog.Debug("================================================================================");

        return (exitCode, stdout, stderr);
    }

    public static async Task<(int exitCode, string stdout, string stderr)> RunSilent(
        string program,
        params object[] args)
    {
        var commandPrinted = GetCommandForPrinting(program, args);
        GenLog.Debug(commandPrinted);

        var command = Command.Run(program, args);

        await command.Task.ConfigureAwait(false);
        var stdout = await command.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await command.StandardError.ReadToEndAsync().ConfigureAwait(false);

        return (command.Result.ExitCode, stdout, stderr);
    }

    public static string RunAndFailIfNotExitZero(string program, params object[] args)
    {
        var (exitCode, stdout, _) = Run(program, args);
        if (exitCode != 0)
        {
            GenLog.Error($"Command failed: {GetCommandForPrinting(program, args)}");
            throw new ConfigurationException($"Exit code was {exitCode}");
        }

        return stdout;
    }

    public static async Task<string> RunSilentAndFailIfNotExitZeroAsync(string program, params object[] args)
    {
        var (exitCode, stdout, stderr) = await RunSilent(program, args).ConfigureAwait(false);
        if (exitCode != 0)
        {
            GenLog.Info(stdout);
            GenLog.Error(stderr);
            GenLog.Error($"Command failed: {GetCommandForPrinting(program, args)}");
            throw new ConfigurationException($"Exit code was {exitCode}");
        }

        return stdout;
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
        var result = Process.GetCurrentProcess().MainModule?.FileName;
        if (result.IsEmpty())
        {
            throw new Exception("Cannot get current script location");
        }

        return result!;
    }

    // Searches local dir PATH environment for command 'name' and returns absolute path to same
    // if found, otherwise null
    public static string? GetExecutableInPath(string name)
    {
        if (!string.IsNullOrWhiteSpace(Path.GetDirectoryName(name)))
        {
            if (File.Exists(name))
            {
                return name;
            }

            throw new ConfigurationException(
                $"Argument to GetExecutableInPath should be a filename. '{name}' does not exist.");
        }

        if (string.IsNullOrWhiteSpace(Path.GetExtension(name)))
        {
            // Iterate over platform specific extensions; cmd, bat, ps1, sh, <none>
            name += ".exe";
        }

        GenLog.Info($"Looking for command {name}");

        string? DoSearch()
        {
            if (File.Exists(name))
            {
                return Path.Combine(Directory.GetCurrentDirectory(), name);
            }

            var fileInScriptDir = Path.Combine(GetScriptDir(), name);
            if (File.Exists(fileInScriptDir))
            {
                return fileInScriptDir;
            }

            var dirsInPath = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
            if (dirsInPath == null || dirsInPath.Length == 0)
            {
                GenLog.Warning("PATH environment variable is empty");
                return null;
            }

            foreach (var dir in dirsInPath)
            {
                var fileInDir = Path.Combine(dir, name);
                if (File.Exists(fileInDir))
                {
                    return fileInDir;
                }
            }

            return null;
        }

        var result = DoSearch();
        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        GenLog.Info($"Found at {result}");
        return result;
    }

    public static int RunPowerShellScriptAndGetExitCode(string scriptRaw, params object[] args)
    {
        var script = GetExecutableInPath(scriptRaw);
        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ConfigurationException($"Script not found {script}");
        }

        var command = new List<object> { "-NoProfile", "-ExecutionPolicy", "Bypass", script };

        if (args.Length > 0)
        {
            command.AddRange(args);
        }

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
        GenLog.Debug($"Env: {name} => {value}");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.ToLowerInvariant() == "false")
        {
            return false;
        }

        if (int.TryParse(value, out var result))
        {
            if (result == 0)
            {
                return false;
            }
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

    private static string GetCommandForPrinting(string program, object[] args)
    {
        var argsPrinted = args.Length == 0 ? "" : string.Join(" ", args);
        var commandPrinted = $"{program} {argsPrinted}";
        return commandPrinted;
    }
}