namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IProgressService
{
    void Start(string stage, int totalWork);
    void SetStage(string stage);
}