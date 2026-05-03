using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Interfaces;

public interface ITransactionProcessor
{
    Task ProcessAsync(BankTransaction transaction);
}