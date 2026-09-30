using System.Runtime.InteropServices;

namespace Vishwapremi;

internal static class InputSimulator
{
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void ExecuteEvents(List<InputEvent> events)
    {
        var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        foreach (var ev in events)
        {
            try
            {
                int targetX = (int)Math.Round(ev.X * bounds.Width) + bounds.X;
                int targetY = (int)Math.Round(ev.Y * bounds.Height) + bounds.Y;

                switch (ev.Type)
                {
                    case "move":
                        SetCursorPos(targetX, targetY);
                        break;
                    case "down":
                        SetCursorPos(targetX, targetY);
                        if (ev.Button == "left") mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)targetX, (uint)targetY, 0, UIntPtr.Zero);
                        else if (ev.Button == "right") mouse_event(MOUSEEVENTF_RIGHTDOWN, (uint)targetX, (uint)targetY, 0, UIntPtr.Zero);
                        else if (ev.Button == "middle") mouse_event(MOUSEEVENTF_MIDDLEDOWN, (uint)targetX, (uint)targetY, 0, UIntPtr.Zero);
                        break;
                    case "up":
                        SetCursorPos(targetX, targetY);
                        if (ev.Button == "left") mouse_event(MOUSEEVENTF_LEFTUP, (uint)targetX, (uint)targetY, 0, UIntPtr.Zero);
                        else if (ev.Button == "right") mouse_event(MOUSEEVENTF_RIGHTUP, (uint)targetX, (uint)targetY, 0, UIntPtr.Zero);
                        else if (ev.Button == "middle") mouse_event(MOUSEEVENTF_MIDDLEUP, (uint)targetX, (uint)targetY, 0, UIntPtr.Zero);
                        break;
                    case "wheel":
                        mouse_event(MOUSEEVENTF_WHEEL, (uint)targetX, (uint)targetY, (uint)ev.Data, UIntPtr.Zero);
                        break;
                    case "keydown":
                        if (ev.Data > 0) keybd_event((byte)ev.Data, 0, 0, UIntPtr.Zero);
                        break;
                    case "keyup":
                        if (ev.Data > 0) keybd_event((byte)ev.Data, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        break;
                    case "char":
                        if (!string.IsNullOrEmpty(ev.Text))
                        {
                            var escaped = ev.Text
                                .Replace("{", "{{}")
                                .Replace("}", "{}}")
                                .Replace("+", "{+}")
                                .Replace("^", "{^}")
                                .Replace("%", "{%}")
                                .Replace("~", "{~}")
                                .Replace("(", "{(}")
                                .Replace(")", "{)}")
                                .Replace("[", "{[}")
                                .Replace("]", "{]}");
                            SendKeys.SendWait(escaped);
                        }
                        break;
                }
            }
            catch { }
        }
    }
}
