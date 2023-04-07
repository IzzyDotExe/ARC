using System.Diagnostics;

namespace Arc.Services;

public class UptimeService : ArcService
{
    
    private readonly Stopwatch _uptime = new Stopwatch();

    public Stopwatch Uptime => _uptime;
    
    public UptimeService() : base("Uptime")
    {
        _uptime.Start();
    }
    
}