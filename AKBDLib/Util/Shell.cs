using System;
using System.Diagnostics;
using System.Text;

namespace AKBDLib.Util
{
    public static class Shell
    {
        // Copied from https://stackoverflow.com/questions/15365038/how-processstartinfo-argument-consider-arguments
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

        public static int RunAndGetExitCode(string program, string argumentString)
        {
            var p = Process.Start(program, argumentString);
            p.WaitForExit();
            return p.ExitCode;
        }

        public static int RunAndGetExitCode(string program, params string[] args)
        {
            var escapedArgs = EscapeArguments(args);
            return RunAndGetExitCode(program, escapedArgs);
        }

        public static void RunAndFailIfNotExitZero(string program, string argumentString)
        {
            var exitCode = RunAndGetExitCode(program, argumentString);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Command: '{program} {argumentString}' returned {exitCode}");
            }
        }

        public static string GetScriptDir()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}