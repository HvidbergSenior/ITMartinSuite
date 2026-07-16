using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Interfaces;

public interface ITransactionScopeClassifier
{
    void Classify(BankTransaction transaction);
}
