using System.Runtime.InteropServices;
using WinRT;

namespace AutoRipDVD;

public static class ComWrappersSupport
{
    public static void InitializeComWrappers()
    {
        if (!ComWrappersSupport.IsInitialized)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            ComWrappersSupport.IsInitialized = true;
        }
    }

    private static bool IsInitialized { get; set; }
}
