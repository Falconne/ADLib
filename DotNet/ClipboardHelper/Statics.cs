using System.Runtime.InteropServices;
using TextCopy;

namespace ClipboardHelper
{
    public static class Statics
    {
        public static async Task DoCopyAsync(string? text)
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
    }
}