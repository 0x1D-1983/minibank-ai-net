using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MiniBank.AI.Telemetry;

public static class TracingExtensions
{
    public static IServiceCollection AddMiniBankTracing(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddOptions<TracingOptions>()
            .Bind(configuration.GetSection(TracingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var options = configuration
            .GetSection(TracingOptions.SectionName)
            .Get<TracingOptions>() ?? new TracingOptions();

        if (!options.Enabled)
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: serviceName,
                serviceVersion: typeof(TracingExtensions).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new AlwaysOnSampler())
                    .AddSource(MiniBankActivitySources.Names)
                    .AddSource("Microsoft.Extensions.AI")
                    .AddSource("Experimental.Microsoft.Extensions.AI")
                    .AddSource("Microsoft.Agents.AI")
                    .AddHttpClientInstrumentation(http => http.RecordException = true)
                    .AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri(options.OtlpEndpoint);
                    });
            });

        return services;
    }
}
