using System.Globalization;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace MiniBank.AI.Workflows;

internal sealed class IntentExecutor(AIAgent intentAgent)
    : Executor<string, BankingIntent>(BankingWorkflow.IntentAgentId)
{
    public override async ValueTask<BankingIntent> HandleAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var response = await intentAgent.RunAsync(message, cancellationToken: cancellationToken);
        return Parse(response, message);
    }

    internal static BankingIntent Parse(AgentResponse response, string question)
    {
        var contents = response.Messages.SelectMany(item => item.Contents).ToList();

        var call = contents.OfType<FunctionCallContent>().LastOrDefault();
        if (call is not null)
            return FromCall(call, question);

        foreach (var result in contents.OfType<FunctionResultContent>())
        {
            if (result.Result is BankingIntent intent)
                return intent with { Question = question };
        }

        return BankingIntent.Read(question);
    }

    private static BankingIntent FromCall(FunctionCallContent call, string question)
        => call.Name switch
        {
            "classify_deposit" => BankingIntent.Deposit(
                question,
                ReadInt64(call.Arguments, "accountNumber") ?? 0,
                ReadDecimal(call.Arguments, "amount") ?? 0),
            "classify_withdraw" => BankingIntent.Withdraw(
                question,
                ReadInt64(call.Arguments, "accountNumber") ?? 0,
                ReadDecimal(call.Arguments, "amount") ?? 0),
            "classify_transfer" => BankingIntent.Transfer(
                question,
                ReadInt64(call.Arguments, "fromAccountNumber") ?? 0,
                ReadInt64(call.Arguments, "toAccountNumber") ?? 0,
                ReadDecimal(call.Arguments, "amount") ?? 0),
            _ => BankingIntent.Read(question)
        };

    private static long? ReadInt64(IDictionary<string, object?>? arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
            return null;

        return value switch
        {
            long number => number,
            int number => number,
            JsonElement element when element.TryGetInt64(out var parsed) => parsed,
            JsonElement element when element.ValueKind == JsonValueKind.String
                && long.TryParse(element.GetString(), out var parsed) => parsed,
            string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                => parsed,
            _ => long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) ? parsed : null
        };
    }

    private static decimal? ReadDecimal(IDictionary<string, object?>? arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
            return null;

        return value switch
        {
            decimal number => number,
            double number => (decimal)number,
            float number => (decimal)number,
            int number => number,
            long number => number,
            JsonElement element when element.TryGetDecimal(out var parsed) => parsed,
            JsonElement element when element.ValueKind == JsonValueKind.String
                && decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                => parsed,
            string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                => parsed,
            _ => decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null
        };
    }
}
