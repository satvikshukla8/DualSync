using System.Runtime.InteropServices;

namespace DualSync.Setup;

public static class WindowsSignature
{
    public static void Verify(string path)
    {
        var text = Marshal.StringToCoTaskMemUni(Path.GetFullPath(path));
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = text };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        Marshal.StructureToPtr(file, pointer, false);
        var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        var data = new TrustData
        {
            Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, UnionChoice = 1,
            File = pointer, StateAction = 1, RevocationChecks = 1
        };
        try
        {
            var result = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            if (result != 0) throw new InvalidDataException($"Windows could not verify the signature of {Path.GetFileName(path)} (0x{result:X8}). Nothing was installed.");
        }
        finally
        {
            data.StateAction = 2; _ = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            Marshal.FreeHGlobal(pointer); Marshal.FreeCoTaskMem(text);
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustFile { public uint Size; public IntPtr Path, File, KnownSubject; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size;
        public IntPtr PolicyCallback, SipClient;
        public uint UiChoice, RevocationChecks, UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData, UrlReference;
        public uint ProviderFlags, UiContext;
        public IntPtr SignatureSettings;
    }
}
