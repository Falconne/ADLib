using ADLib.Logging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using TextCopy;
using Clipboard = System.Windows.Clipboard;

// ReSharper disable UseConfigureAwaitFalse

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
        Func<string?, string, string?, Task> onTextChanged,
        CancellationToken cancellationToken = default)
    {
        string? oldClipText = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var (sourceUrl, clipText, rawHtml) =
                await GetCurrentTextContent(cancellationToken);

            if (clipText != null && clipText != oldClipText)
            {
                GenLog.Info($"Caught new clipboard test: {clipText}");
                await onTextChanged(sourceUrl, clipText, rawHtml);
            }

            if (clipText != null)
            {
                oldClipText = clipText;
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    public static Task<(string? sourceUrl, string? clipText, string? rawHtml)> GetCurrentTextContent(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return RetryClipboardOperationAsync(GetCurrentTextContentUnsafe, cancellationToken);
        }
        catch (Exception)
        {
            return Task
                .FromResult<(string?, string?, string?)>((null, null, null));
        }
    }

    public static Task<BitmapSource?> GetCurrentImageContent(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return RetryClipboardOperationAsync(
                _ => Task.FromResult(GetClipboardImageUnsafe()),
                cancellationToken);
        }
        catch (Exception)
        {
            return Task.FromResult<BitmapSource?>(null);
        }
    }

    private static BitmapSource? GetClipboardImageUnsafe()
    {
        return Clipboard.ContainsImage() ? Clipboard.GetImage() : null;
    }

    private static async Task<T> RetryClipboardOperationAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var tries = 0;
        while (++tries < 10)
        {
            if (tries > 1)
            {
                GenLog.Debug($"Retrying clipboard operation, attempt {tries}...");
            }

            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception e)
            {
                GenLog.Debug($"Ignoring clipboard error: {e.Message}");
                await Task.Delay(200, cancellationToken);
            }
        }

        throw new Exception("Failed to get clipboard content after multiple attempts.");
    }

    private static async Task<(string? sourceUrl, string? clipText, string? rawHtml)>
        GetCurrentTextContentUnsafe(
            CancellationToken cancellationToken = default)
    {
        var clipText = await ClipboardService.GetTextAsync(cancellationToken);
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