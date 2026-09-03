using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MiniBank.AI.Telemetry;

internal sealed class TracingAIFunction(AIFunction inner, ILogger logger) : DelegatingAIFunction(inner)
{
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        using var activity = MiniBankActivitySources.Tools.StartActivity("tools.execute");
        activity?.SetTag("tool.name", Name);

        var sw = Stopwatch.StartNew();
        var input = JsonSerializer.Serialize(arguments.ToDictionary(pair => pair.Key, pair => pair.Value));

        logger.LogDebug("Executing tool '{ToolName}' with input: {Input}", Name, input);

        try
        {
            var result = await base.InvokeCoreAsync(arguments, cancellationToken);
            sw.Stop();
            activity?.SetTag("tool.success", true);

            logger.LogInformation(
                "Tool '{ToolName}' completed in {ElapsedMs}ms",
                Name, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            activity.RecordError(ex);
            activity?.SetTag("tool.success", false);
            logger.LogError(
                ex,
                "Tool '{ToolName}' threw an unhandled exception after {ElapsedMs}ms",
                Name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
