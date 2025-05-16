using ADLib.Logging;
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
                await ClipboardService.SetTextAsync(text ?? "").ConfigureAwait(false);
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

                await Task.Delay(100).ConfigureAwait(false);
            }
        }
    }

    public static async Task Monitor(
        Func<string?, string, string?, Task> onTextChanged,
        CancellationToken cancellationToken = default)
    {
        string? oldClipText = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var (sourceUrl, clipText, rawHtml) =
                await GetCurrentTextContent(cancellationToken).ConfigureAwait(false);

            if (clipText != null && clipText != oldClipText)
            {
                GenLog.Info($"Caught new clipboard test: {clipText}");
                await onTextChanged(sourceUrl, clipText, rawHtml).ConfigureAwait(false);
            }

            if (clipText != null)
            {
                oldClipText = clipText;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task<(string? sourceUrl, string? clipText, string? rawHtml)> GetCurrentTextContent(
        CancellationToken cancellationToken = default)

    {
        while (true)
        {
            try
            {
                return await GetCurrentTextContentUnsafe(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                GenLog.Debug($"Ignoring clipboard error: {e.Message}");
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<(string? sourceUrl, string? clipText, string? rawHtml)>
        GetCurrentTextContentUnsafe(
            CancellationToken cancellationToken = default)
    {
        var clipText = await ClipboardService.GetTextAsync(cancellationToken).ConfigureAwait(false);
        string? sourceUrl = null;
        string? rawHtml = null;
        if (Clipboard.ContainsText(TextDataFormat.Html))
        {
            rawHtml = Clipboard.GetText(TextDataFormat.Html);
            var sourceMatch = _sourceRegex.Match(rawHtml);
            if (sourceMatch.Success)
            {
                sourceUrl = sourceMatch.Groups[1].Value;
            }
        }

        return (sourceUrl, clipText, rawHtml);
    }
}