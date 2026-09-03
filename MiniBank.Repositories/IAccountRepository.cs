namespace Banking.Repositories;

using System.Collections.Generic;
using System.Threading.Tasks;
using MiniBank.Domain.Models;

public interface IAccountRepository
{
    Task AddAccountAsync(Account account);
    Task<Account?> FindByIdAsync(long accountNumber);
    Task<List<Account>> AllAsync();
    Task<List<Account>> FindByOwnerAsync(string owner);
    Task UpdateAccountAsync(Account account);
}