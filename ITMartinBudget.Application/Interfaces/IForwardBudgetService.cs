using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Interfaces;

public interface IForwardBudgetService
{
    Task<ForwardBudgetViewModel> BuildAsync();
    
}