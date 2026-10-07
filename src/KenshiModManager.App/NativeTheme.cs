using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace KenshiModManager.App;
public partial class MainWindow
{
    private void ApplyNativeDarkCaption()
    {
        var dark = 1;
        var result = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
        if(result != 0) System.Diagnostics.Trace.TraceInformation("Native dark caption unavailable, HRESULT={0:X8}; using system caption.", result);
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
