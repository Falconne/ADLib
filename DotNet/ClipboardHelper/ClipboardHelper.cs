﻿using ADLib.Logging;
using System.Runtime.InteropServices;
using TextCopy;

namespace ClipboardHelper;

public static class ClipboardHelper
{
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
        Func<string, Task> onTextChanged,
        CancellationToken cancellationToken = default)
    {
        string? oldClipText = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            string? clipText = null;
            try
            {
                clipText = await ClipboardService.GetTextAsync(cancellationToken);
            }
            catch (Exception e)
            {
                GenLog.Error($"Ignoring clipboard error: {e.Message}");
            }

            if (clipText != null && clipText != oldClipText)
            {
                GenLog.Info($"Caught new clipboard test: {clipText}");
                await onTextChanged(clipText);
            }

            if (clipText != null)
            {
                oldClipText = clipText;
            }

            await Task.Delay(100, cancellationToken);
        }
    }
}