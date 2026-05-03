using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Interfaces;

public interface ITransactionGroupingService
{
    string GetGroupingKey(BankTransaction tx);
}