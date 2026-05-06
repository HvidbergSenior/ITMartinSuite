using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Application.Interfaces;

public interface ICategoryService
{
    Task<Category> DetectAsync(string groupingKey);
}