using System.Runtime.InteropServices;
using Photino.NET;

namespace NetworkDiagnostics.Desktop;

internal static class MacWindowChrome
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private const string ToggleZoomMessage = "macos.window.toggleZoom";
    private static readonly nuint FullSizeContentViewMask = (nuint)1 << 15;
    private static readonly nuint HiddenTitleVisibility = 1;
    private const double TrafficLightVerticalOffset = -7.0;
    private static readonly Dictionary<IntPtr, NativePoint[]> TrafficLightOrigins = new();
    private static bool nativeMessageHandlerRegistered;

    public static void RegisterNativeMessageHandler(PhotinoWindow photinoWindow)
    {
        if (!OperatingSystem.IsMacOS() || nativeMessageHandlerRegistered)
        {
            return;
        }

        nativeMessageHandlerRegistered = true;
        photinoWindow.RegisterWebMessageReceivedHandler((_, message) =>
        {
            if (string.Equals(message, ToggleZoomMessage, StringComparison.Ordinal))
            {
                TryToggleZoom();
            }
        });
    }

    public static bool TryEnableUnifiedTitlebar()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            var window = FindPhotinoWindow();
            if (window == IntPtr.Zero)
            {
                Console.Error.WriteLine("macOS unified title bar is waiting for the Photino NSWindow to become available.");
                return false;
            }

            var styleMask = SendNUInt(window, "styleMask");
            SendVoidNUInt(window, "setStyleMask:", styleMask | FullSizeContentViewMask);
            SendVoidBool(window, "setTitlebarAppearsTransparent:", true);
            SendVoidNUInt(window, "setTitleVisibility:", HiddenTitleVisibility);
            SendVoidBool(window, "setMovableByWindowBackground:", true);
            CenterTrafficLights(window);
            return true;
        }
        catch (Exception error)
        {
            // The normal Photino title bar remains usable if AppKit interop ever changes.
            Console.Error.WriteLine($"macOS unified title bar could not be enabled: {error.Message}");
            return false;
        }
    }

    public static bool TryToggleZoom()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            var window = FindPhotinoWindow();
            if (window == IntPtr.Zero)
            {
                return false;
            }

            // performZoom: is the native title-bar zoom/restore action. Using AppKit
            // keeps macOS window sizing semantics instead of inventing a WebView size.
            SendVoidObject(window, "performZoom:", IntPtr.Zero);
            return true;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"macOS title-bar zoom could not be toggled: {error.Message}");
            return false;
        }
    }

    private static IntPtr FindPhotinoWindow()
    {
        var applicationClass = objc_getClass("NSApplication");
        if (applicationClass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var application = SendObject(applicationClass, "sharedApplication");
        if (application == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        // WindowCreated fires before AppKit necessarily designates the new window as
        // key/main. Prefer those stable identities once available, then fall back to
        // NSApplication.windows so the title-bar treatment can still be applied before
        // the first visible frame.
        var window = SendObject(application, "keyWindow");
        if (window != IntPtr.Zero)
        {
            return window;
        }

        window = SendObject(application, "mainWindow");
        if (window != IntPtr.Zero)
        {
            return window;
        }

        var windows = SendObject(application, "windows");
        if (windows == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var count = SendNUInt(windows, "count");
        if (count == 0)
        {
            return IntPtr.Zero;
        }

        // Photino currently owns one app window. Use the newest AppKit window as the
        // creation-time fallback; the focus callback below retries against keyWindow.
        return SendObjectNUInt(windows, "objectAtIndex:", count - 1);
    }

    private static void CenterTrafficLights(IntPtr window)
    {
        // AppKit keeps the standard traffic lights at their conventional title-bar Y
        // coordinate even when content extends into the title bar. Our unified toolbar
        // is taller, so preserve the native buttons and move the group down by seven
        // points to align their centers with the toolbar content (Chrome-style).
        NativePoint[] origins;
        lock (TrafficLightOrigins)
        {
            if (!TrafficLightOrigins.TryGetValue(window, out origins!))
            {
                origins = new NativePoint[3];
                for (var buttonType = 0; buttonType < origins.Length; buttonType++)
                {
                    var button = SendObjectNUInt(window, "standardWindowButton:", (nuint)buttonType);
                    origins[buttonType] = button == IntPtr.Zero
                        ? default
                        : SendPoint(button, "frameOrigin");
                }
                TrafficLightOrigins[window] = origins;
            }
        }

        for (var buttonType = 0; buttonType < origins.Length; buttonType++)
        {
            var button = SendObjectNUInt(window, "standardWindowButton:", (nuint)buttonType);
            if (button == IntPtr.Zero)
            {
                continue;
            }

            var origin = origins[buttonType];
            SendVoidPoint(
                button,
                "setFrameOrigin:",
                new NativePoint(origin.X, origin.Y + TrafficLightVerticalOffset));
        }
    }

    private static IntPtr SendObject(IntPtr receiver, string selector) =>
        objc_msgSend_IntPtr(receiver, sel_registerName(selector));

    private static IntPtr SendObjectNUInt(IntPtr receiver, string selector, nuint value) =>
        objc_msgSend_IntPtr_NUInt(receiver, sel_registerName(selector), value);

    private static nuint SendNUInt(IntPtr receiver, string selector) =>
        objc_msgSend_NUInt(receiver, sel_registerName(selector));

    private static NativePoint SendPoint(IntPtr receiver, string selector) =>
        objc_msgSend_Point(receiver, sel_registerName(selector));

    private static void SendVoidNUInt(IntPtr receiver, string selector, nuint value) =>
        objc_msgSend_Void_NUInt(receiver, sel_registerName(selector), value);

    private static void SendVoidBool(IntPtr receiver, string selector, bool value) =>
        objc_msgSend_Void_Bool(receiver, sel_registerName(selector), value);

    private static void SendVoidPoint(IntPtr receiver, string selector, NativePoint value) =>
        objc_msgSend_Void_Point(receiver, sel_registerName(selector), value);

    private static void SendVoidObject(IntPtr receiver, string selector, IntPtr value) =>
        objc_msgSend_Void_IntPtr(receiver, sel_registerName(selector), value);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public NativePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X;
        public double Y;
    }

    [DllImport(ObjectiveCLibrary, CharSet = CharSet.Ansi)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjectiveCLibrary, CharSet = CharSet.Ansi)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_NUInt(IntPtr receiver, IntPtr selector, nuint value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern nuint objc_msgSend_NUInt(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern NativePoint objc_msgSend_Point(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_NUInt(IntPtr receiver, IntPtr selector, nuint value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_Bool(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_Point(IntPtr receiver, IntPtr selector, NativePoint value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_IntPtr(IntPtr receiver, IntPtr selector, IntPtr value);
}
