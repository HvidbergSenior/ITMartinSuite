using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Interfaces;

public interface IFamilyPlanningService
{
    Task<FamilyPlanningViewModel>
        BuildAsync();
}