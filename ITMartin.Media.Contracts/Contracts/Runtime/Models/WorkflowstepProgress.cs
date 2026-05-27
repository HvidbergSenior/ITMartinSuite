namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class WorkflowStepProgress
{
    public int Current { get; set; }

    public int Total { get; set; }

    public string Message { get; set; } = string.Empty;

    public double Percent =>
        Total == 0
            ? 0
            : (double)Current / Total * 100;
}