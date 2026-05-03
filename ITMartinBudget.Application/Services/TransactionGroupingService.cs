using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain;
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Services;

public class TransactionGroupingService : ITransactionGroupingService
{
    public string GetGroupingKey(BankTransaction tx)
    {
        return $"{tx.Date:yyyy-MM-dd}_{tx.Amount}_{tx.Description}";
    }
}