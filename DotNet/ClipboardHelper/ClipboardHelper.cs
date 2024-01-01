﻿using ADLib.Logging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using TextCopy;
using Clipboard = System.Windows.Clipboard;

namespace ClipboardHelper;

public static class ClipboardHelper
{
    private static readonly Regex _sourceRegex = new(
        @"SourceURL:\s*(http.+?)(<|\s|""|$)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static async Task CopyToClipboardAsync(string? text)
    {
        while (true)
        {
            try
            {
                await ClipboardService.SetTextAsync(text ?? "");
                return;
            }
            catch (COMException e)
            {
                // ReSharper disable once IdentifierTypo
                // ReSharper disable once InconsistentNaming
                const uint CLIPBRD_E_CANT_OPEN = 0x800401D0;
                if ((uint)e.ErrorCode != CLIPBRD_E_CANT_OPEN)
                {
                    throw;
                }

                await Task.Delay(100);
            }
        }
    }

    public static async Task Monitor(
        Func<string?, string, Task> onTextChanged,
        CancellationToken cancellationToken = default)
    {
        string? oldClipText = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var (sourceUrl, clipText) = await GetCurrentContentSafely(cancellationToken);

            if (clipText != null && clipText != oldClipText)
            {
                GenLog.Info($"Caught new clipboard test: {clipText}");
                await onTextChanged(sourceUrl, clipText);
            }

            if (clipText != null)
            {
                oldClipText = clipText;
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    public static async Task<(string? sourceUrl, string? clipText)> GetCurrentContentSafely(
        CancellationToken cancellationToken = default)

    {
        while (true)
        {
            try
            {
                return await GetCurrentContent(cancellationToken);
            }
            catch (Exception e)
            {
                GenLog.Error($"Ignoring clipboard error: {e.Message}");
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    public static async Task<(string? sourceUrl, string? clipText)> GetCurrentContent(
        CancellationToken cancellationToken = default)
    {
        var clipText = await ClipboardService.GetTextAsync(cancellationToken);
        string? sourceUrl = null;
        if (Clipboard.ContainsText(TextDataFormat.Html))
        {
            clipText = Clipboard.GetText(TextDataFormat.Html);
            var sourceMatch = _sourceRegex.Match(clipText);
            if (sourceMatch.Success)
            {
                sourceUrl = sourceMatch.Groups[1].Value;
            }
        }

        return (sourceUrl, clipText);
    }
}