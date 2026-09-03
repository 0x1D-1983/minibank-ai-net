using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace MiniBank.AI.Tests.Support;

internal static class AgentAssert
{
    public static void ChoseTool(RecordingChatClient chat, string toolName)
    {
        Assert.True(
            chat.InvokedCalls.Count > 0,
            "The model did not request any tool.");
        Assert.True(
            chat.InvokedCalls.Any(call => call.Name == toolName),
            $"Expected tool '{toolName}', but the model requested: {string.Join(", ", chat.InvokedCalls.Select(call => call.Name))}");
        Assert.True(
            chat.ToolResults.Count > 0,
            $"The model requested '{toolName}' but the tool was not executed.");
    }

    public static void DidNotChoose(RecordingChatClient chat, params string[] toolNames)
    {
        foreach (var name in toolNames)
        {
            Assert.DoesNotContain(
                chat.InvokedCalls,
                call => string.Equals(call.Name, name, StringComparison.Ordinal));
        }
    }

    public static void ReceivedArgument(RecordingChatClient chat, string toolName, string name, object expected)
    {
        var call = chat.InvokedCalls.LastOrDefault(candidate => candidate.Name == toolName);
        Assert.NotNull(call);
        Assert.NotNull(call.Arguments);
        Assert.True(call.Arguments.ContainsKey(name), $"Missing argument '{name}' on '{toolName}'.");
        Assert.True(
            ValuesEqual(call.Arguments[name], expected),
            $"Argument '{name}' on '{toolName}' was '{call.Arguments[name]}', expected '{expected}'.");
    }

    public static void ReceivedNoArguments(RecordingChatClient chat, string toolName)
    {
        var call = chat.InvokedCalls.LastOrDefault(candidate => candidate.Name == toolName);
        Assert.NotNull(call);
        Assert.True(
            call.Arguments is null || call.Arguments.Count == 0,
            $"Expected no arguments on '{toolName}', got: {string.Join(", ", call.Arguments!.Keys)}");
    }

    public static void AnswerContainsFacts(string answer, params object[] facts)
    {
        Assert.False(string.IsNullOrWhiteSpace(answer), "The agent produced an empty answer.");

        foreach (var fact in facts)
        {
            if (fact is decimal amount)
            {
                Assert.True(
                    ContainsAmount(answer, amount),
                    $"Expected amount {amount.ToString(CultureInfo.InvariantCulture)} in: {answer}");
                continue;
            }

            if (fact is int or long)
            {
                var token = Convert.ToString(fact, CultureInfo.InvariantCulture)!;
                Assert.True(
                    ContainsToken(answer, token),
                    $"Expected '{token}' in: {answer}");
                continue;
            }

            var text = Convert.ToString(fact, CultureInfo.InvariantCulture) ?? string.Empty;
            Assert.Contains(text, answer, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void LookedUpOwner(RecordingAccountRepository repository, string owner)
    {
        Assert.Contains(
            repository.FindByOwnerArgs,
            value => value.Equals(owner, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAmount(string text, decimal amount)
    {
        var normalized = NormalizeMoney(text);
        string[] forms =
        [
            amount.ToString(CultureInfo.InvariantCulture),
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            amount.ToString("#,##0.00", CultureInfo.InvariantCulture)
        ];

        if (forms.Any(form => text.Contains(form, StringComparison.Ordinal)
                              || normalized.Contains(form.Replace(",", ""), StringComparison.Ordinal)))
        {
            return true;
        }

        if (amount == decimal.Truncate(amount))
        {
            var whole = decimal.Truncate(amount).ToString(CultureInfo.InvariantCulture);
            return ContainsToken(text, whole) || normalized.Contains(whole, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool ContainsToken(string text, string token)
        => Regex.IsMatch(text, $@"\b{Regex.Escape(token)}\b");

    private static string NormalizeMoney(string text)
        => text.Replace(",", "", StringComparison.Ordinal)
            .Replace("£", "", StringComparison.Ordinal)
            .Replace("GBP", "", StringComparison.OrdinalIgnoreCase);

    private static bool ValuesEqual(object? actual, object expected)
    {
        if (actual is null)
            return false;

        if (expected.Equals(actual))
            return true;

        if (actual is JsonElement element)
        {
            if (expected is long expectedLong)
            {
                return (element.TryGetInt64(out var asLong) && asLong == expectedLong)
                    || (element.TryGetInt32(out var asInt) && asInt == expectedLong)
                    || (element.ValueKind == JsonValueKind.String
                        && long.TryParse(element.GetString(), out var parsed)
                        && parsed == expectedLong);
            }

            if (expected is string expectedString)
            {
                var value = element.ValueKind == JsonValueKind.String
                    ? element.GetString()
                    : element.ToString();
                return string.Equals(value, expectedString, StringComparison.OrdinalIgnoreCase);
            }
        }

        return string.Equals(
            Convert.ToString(actual, CultureInfo.InvariantCulture),
            Convert.ToString(expected, CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }
}
