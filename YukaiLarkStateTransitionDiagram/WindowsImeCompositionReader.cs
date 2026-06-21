namespace YukaiLarkStateTransitionDiagram;

using System;
using System.Runtime.InteropServices;
using System.Text;

internal static class WindowsImeCompositionReader
{
    private const int GcsCompositionString = 0x0008;

    public static string GetCompositionString()
    {
        var hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero)
        {
            return string.Empty;
        }

        var inputContext = ImmGetContext(hwnd);
        if (inputContext == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            var byteLength = ImmGetCompositionString(inputContext, GcsCompositionString, null, 0);
            if (byteLength <= 0)
            {
                return string.Empty;
            }

            var buffer = new byte[byteLength];
            var copied = ImmGetCompositionString(inputContext, GcsCompositionString, buffer, buffer.Length);
            return copied > 0 ? Encoding.Unicode.GetString(buffer, 0, copied) : string.Empty;
        }
        finally
        {
            _ = ImmReleaseContext(hwnd, inputContext);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hwnd);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr inputContext);

    [DllImport("imm32.dll", EntryPoint = "ImmGetCompositionStringW")]
    private static extern int ImmGetCompositionString(IntPtr inputContext, int index, byte[]? buffer, int bufferLength);
}