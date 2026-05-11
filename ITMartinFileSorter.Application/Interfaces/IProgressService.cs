using System.Diagnostics;

namespace ITMartinFileSorter.Application.Interfaces;

public interface IProgressService
{
    void Start(string stage, int totalWork);
    void SetStage(string stage);
}