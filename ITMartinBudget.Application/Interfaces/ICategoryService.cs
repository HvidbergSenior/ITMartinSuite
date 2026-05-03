using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Interfaces;

public interface ICategoryService
{
    Task<SubCategory> DetectAsync(string groupingKey, BankTransaction tx);
}