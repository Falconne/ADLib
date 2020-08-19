using CommandLine;
using System;

namespace ZApplication
{
    public static class Helpers
    {
        public static void ParseArguments<T>(Action<T> actionWithParsed, params string[] args)
        {
            Parser.Default.ParseArguments<T>(args)

                .WithParsed(actionWithParsed)
                .WithNotParsed(e => { Environment.Exit(1); });

        }

    }
}