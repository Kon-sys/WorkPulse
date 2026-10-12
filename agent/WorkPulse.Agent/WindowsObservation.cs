using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WorkPulse.Agent;

public sealed record Observation(string? Application,uint LastInputTick,bool Idle);

/// <summary>Process identity and last-input age only; no window titles, URLs, key contents or screenshots.</summary>
public static class WindowsObservation
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo { public uint Size;public uint Time; }
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    public static Observation Read(int idleSeconds)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Collector requires an interactive Windows session.");
        var info=new LastInputInfo {Size=(uint)Marshal.SizeOf<LastInputInfo>()};
        if(!GetLastInputInfo(ref info)) throw new InvalidOperationException("Cannot read last-input state.");
        var age=unchecked((uint)Environment.TickCount-info.Time);
        var window=GetForegroundWindow();string? application=null;
        if(window!=IntPtr.Zero)
        {
            GetWindowThreadProcessId(window,out var id);
            try {using var process=Process.GetProcessById((int)id);application=process.ProcessName.ToLowerInvariant()+".exe";}
            catch(ArgumentException) { }
            catch(System.ComponentModel.Win32Exception) { }
            catch(InvalidOperationException) { }
        }
        if(application is not null && (application.Length>128 || application.Any(c=>!char.IsLetterOrDigit(c) && c is not ('.' or '-' or '_')))) application=null;
        return new(application,info.Time,age>=(uint)idleSeconds*1000);
    }
}
