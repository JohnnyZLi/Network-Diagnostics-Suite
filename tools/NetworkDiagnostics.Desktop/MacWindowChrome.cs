using System.Runtime.InteropServices;

namespace NetworkDiagnostics.Desktop;

internal static class MacWindowChrome
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private static readonly nuint FullSizeContentViewMask = (nuint)1 << 15;
    private static readonly nuint HiddenTitleVisibility = 1;

    public static bool TryEnableUnifiedTitlebar()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            var applicationClass = objc_getClass("NSApplication");
            if (applicationClass == IntPtr.Zero)
            {
                return false;
            }

            var application = SendObject(applicationClass, "sharedApplication");
            if (application == IntPtr.Zero)
            {
                return false;
            }

            var window = FindPhotinoWindow(application);
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
            return true;
        }
        catch (Exception error)
        {
            // The normal Photino title bar remains usable if AppKit interop ever changes.
            Console.Error.WriteLine($"macOS unified title bar could not be enabled: {error.Message}");
            return false;
        }
    }

    private static IntPtr FindPhotinoWindow(IntPtr application)
    {
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

    private static IntPtr SendObject(IntPtr receiver, string selector) =>
        objc_msgSend_IntPtr(receiver, sel_registerName(selector));

    private static IntPtr SendObjectNUInt(IntPtr receiver, string selector, nuint value) =>
        objc_msgSend_IntPtr_NUInt(receiver, sel_registerName(selector), value);

    private static nuint SendNUInt(IntPtr receiver, string selector) =>
        objc_msgSend_NUInt(receiver, sel_registerName(selector));

    private static void SendVoidNUInt(IntPtr receiver, string selector, nuint value) =>
        objc_msgSend_Void_NUInt(receiver, sel_registerName(selector), value);

    private static void SendVoidBool(IntPtr receiver, string selector, bool value) =>
        objc_msgSend_Void_Bool(receiver, sel_registerName(selector), value);

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
    private static extern void objc_msgSend_Void_NUInt(IntPtr receiver, IntPtr selector, nuint value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_Void_Bool(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.I1)] bool value);
}
