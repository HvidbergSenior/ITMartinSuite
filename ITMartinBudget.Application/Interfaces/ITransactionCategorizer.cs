using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Interfaces;

public interface ITransactionCategorizer
{
    void Categorize(BankTransaction transaction);
}