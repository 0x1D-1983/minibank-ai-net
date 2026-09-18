using MiniBank.Domain.Models;
using Banking.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace MiniBank.AI.Tools;

internal static class OwnerResolver
{
    /// <summary>
    /// Resolves an owner name to accounts, scoped to the current customer's
    /// accounts only. Uses AuthorizedBank to enforce authorization.
    /// </summary>
    public static async Task<List<Account>> ResolveAsync(AuthorizedBank bank, string owner)
    {
        if (string.IsNullOrWhiteSpace(owner))
            return [];

        var all = await bank.GetAllAccountsAsync();
        if (all.Count == 0)
            return [];

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
