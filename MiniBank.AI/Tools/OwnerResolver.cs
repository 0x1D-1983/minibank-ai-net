using Banking.Domain.Models;
using Banking.Services;

namespace MiniBank.AI.Tools;

internal static class OwnerResolver
{
    public static async Task<List<Account>> ResolveAsync(Bank bank, string owner)
    {
        if (string.IsNullOrWhiteSpace(owner))
            return [];

        var exact = await bank.GetAccountsByOwnerAsync(owner);
        if (exact.Count > 0)
            return exact;

        var all = await bank.GetAllAccountsAsync();
        var matches = all
            .GroupBy(account => account.Owner, StringComparer.OrdinalIgnoreCase)
            .Where(group => Matches(group.Key, owner))
            .ToList();

        return matches.Count == 1 ? matches[0].ToList() : [];
    }

    private static bool Matches(string fullName, string query)
    {
        if (fullName.Equals(query, StringComparison.OrdinalIgnoreCase))
            return true;

        var tokens = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(token => token.Equals(query, StringComparison.OrdinalIgnoreCase));
    }
}
