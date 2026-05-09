using System.Diagnostics;
using System.Runtime.InteropServices;
using CloudMusicPlaylistSearch.Core.Models;
using CloudMusicPlaylistSearch.Core.Search;

namespace CloudMusicPlaylistSearch.Infrastructure.Playback;

public sealed class CloudMusicTrackActivator
{
    private const string CloudMusicProcessName = "cloudmusic";

    private const double PlaylistProbeXRatio = 0.925;
    private const double PlaylistProbePrimaryYRatio = 0.18;
    private const double PlaylistProbeSecondaryYRatio = 0.35;
    private const double PlaylistFocusXRatio = 0.855;
    private const double PlaylistFocusYRatio = 0.285;

    public async Task<TrackActivationResult> ActivateTrackAsync(
        PlaylistTrack track,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        var process = FindCloudMusicProcess();
        if (process is null)
        {
            return new TrackActivationResult(false, "CloudMusic is not running.");
        }

        if (!TryGetWindowBounds(process.MainWindowHandle, out var windowBounds))
        {
            return new TrackActivationResult(false, "CloudMusic main window is not available.");
        }

        var titleBefore = process.MainWindowTitle;

        ActivateWindow(process.MainWindowHandle);
        await Task.Delay(120, cancellationToken);

        if (!IsPlaylistPanelOpen(windowBounds))
        {
            return new TrackActivationResult(
                false,
                "The current playlist panel is not open in CloudMusic. Open it first, then try again.");
        }

        ClickNormalized(windowBounds, PlaylistFocusXRatio, PlaylistFocusYRatio);
        await Task.Delay(80, cancellationToken);

        SendKeys(CloudMusicTrackActivationPlan.BuildNavigationKeys(track.DisplayIndex));
        await Task.Delay(320, cancellationToken);

        process.Refresh();
        var titleAfter = process.MainWindowTitle;
        if (LooksLikeTrackActivated(track, titleBefore, titleAfter))
        {
            return new TrackActivationResult(true, $"Switched to {track.Name} - {track.Artist}.");
        }

        return new TrackActivationResult(
            false,
            "The activation command was sent, but playback could not be verified. Keep the current playlist panel visible and try again.");
    }

    private static Process? FindCloudMusicProcess()
    {
        return Process
            .GetProcessesByName(CloudMusicProcessName)
            .FirstOrDefault(candidate => candidate.MainWindowHandle != IntPtr.Zero);
    }

    private static bool TryGetWindowBounds(IntPtr handle, out WindowBounds bounds)
    {
        bounds = default;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(handle, out var rect))
        {
            return false;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        bounds = new WindowBounds(rect.Left, rect.Top, width, height);
        return true;
    }

    private static void ActivateWindow(IntPtr handle)
    {
        NativeMethods.ShowWindow(handle, 9);
        NativeMethods.SetForegroundWindow(handle);
    }

    private static bool IsPlaylistPanelOpen(WindowBounds bounds)
    {
        var probePoints = new[]
        {
            bounds.Normalize(PlaylistProbeXRatio, PlaylistProbePrimaryYRatio),
            bounds.Normalize(PlaylistProbeXRatio, PlaylistProbeSecondaryYRatio),
        };

        var brightSamples = 0;
        foreach (var point in probePoints)
        {
            var brightness = ReadBrightness(point.X, point.Y);
            if (brightness >= 170)
            {
                brightSamples++;
            }
        }

        return brightSamples >= 1;
    }

    private static int ReadBrightness(int x, int y)
    {
        var deviceContext = NativeMethods.GetDC(IntPtr.Zero);
        if (deviceContext == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var color = NativeMethods.GetPixel(deviceContext, x, y);
            var red = (int)(color & 0x000000FF);
            var green = (int)((color & 0x0000FF00) >> 8);
            var blue = (int)((color & 0x00FF0000) >> 16);
            return (red + green + blue) / 3;
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, deviceContext);
        }
    }

    private static void ClickNormalized(WindowBounds bounds, double xRatio, double yRatio)
    {
        var point = bounds.Normalize(xRatio, yRatio);
        NativeMethods.SetCursorPos(point.X, point.Y);
        NativeMethods.mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        NativeMethods.mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
    }

    private static void SendKeys(IReadOnlyList<ushort> keys)
    {
        if (keys.Count == 0)
        {
            return;
        }

        var inputs = new NativeMethods.INPUT[keys.Count * 2];
        var cursor = 0;

        foreach (var key in keys)
        {
            inputs[cursor++] = NativeMethods.CreateKeyboardInput(key, keyUp: false);
            inputs[cursor++] = NativeMethods.CreateKeyboardInput(key, keyUp: true);
        }

        var sent = NativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<NativeMethods.INPUT>());

        if (sent != inputs.Length)
        {
            throw new InvalidOperationException("Failed to send all keyboard inputs to CloudMusic.");
        }
    }

    private static bool LooksLikeTrackActivated(PlaylistTrack track, string titleBefore, string titleAfter)
    {
        var normalizedTrackName = SearchTextNormalizer.Normalize(track.Name);
        var normalizedTrackArtist = SearchTextNormalizer.Normalize(track.Artist);
        var normalizedAfter = SearchTextNormalizer.Normalize(titleAfter);
        var normalizedBefore = SearchTextNormalizer.Normalize(titleBefore);

        if (normalizedAfter.Contains(normalizedTrackName, StringComparison.Ordinal)
            || normalizedAfter.Contains(normalizedTrackArtist, StringComparison.Ordinal))
        {
            return true;
        }

        return normalizedBefore.Contains(normalizedTrackName, StringComparison.Ordinal)
            && normalizedAfter == normalizedBefore;
    }

    private readonly record struct WindowBounds(int Left, int Top, int Width, int Height)
    {
        public NativeMethods.POINT Normalize(double xRatio, double yRatio)
        {
            var x = Left + (int)Math.Round(Width * xRatio);
            var y = Top + (int)Math.Round(Height * yRatio);
            return new NativeMethods.POINT(x, y);
        }
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("gdi32.dll")]
        public static extern uint GetPixel(IntPtr hdc, int x, int y);

        public static INPUT CreateKeyboardInput(ushort virtualKey, bool keyUp)
        {
            return new INPUT
            {
                type = 1,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = virtualKey,
                        wScan = 0,
                        dwFlags = keyUp ? 0x0002u : 0u,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            };
        }
    }
}