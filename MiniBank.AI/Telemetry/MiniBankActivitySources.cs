using System.Diagnostics;

namespace MiniBank.AI.Telemetry;

public static class MiniBankActivitySources
{
    public static readonly ActivitySource Agent = new("MiniBank.Agent");
    public static readonly ActivitySource Tools = new("MiniBank.Tools");

    public static readonly string[] Names =
    [
        Agent.Name,
        Tools.Name
    ];
}

public static class ActivityExtensions
{
    public static void RecordError(this Activity? activity, Exception exception)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
    }
}
