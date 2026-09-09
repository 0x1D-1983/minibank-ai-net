using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

namespace MiniBank.AI.Agents;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    [Required]
    public string Endpoint { get; set; } = "";

    [Required]
    public string Model { get; set; } = "";

    public static OllamaOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetRequiredSection(SectionName).Get<OllamaOptions>()
            ?? throw new InvalidOperationException($"Configuration section '{SectionName}' is invalid.");

        ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Model);
        return options;
    }
}
