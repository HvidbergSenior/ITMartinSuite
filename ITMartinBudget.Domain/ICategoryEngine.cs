namespace ITMartinBudget.Domain;

public interface ICategoryEngine
{
    SubCategory Detect(string text);
}