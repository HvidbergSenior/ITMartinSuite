using System.Diagnostics;

namespace ITMartinFileSorter.Application.Services;

public class ProgressService
{
    public ProgressInfo Info { get; private set; } = new();

    public event Action? OnChange;

    Stopwatch _timer = new();

    int _lastWorkDone = 0;
    DateTime _lastUpdate = DateTime.UtcNow;

    public void Start(string stage, int totalWork)
    {
        Info = new ProgressInfo
        {
            Stage = stage,
            TotalWork = totalWork,
            WorkDone = 0
        };

        _timer.Restart();
        _lastWorkDone = 0;
        _lastUpdate = DateTime.UtcNow;

        Notify();
    }

    public void SetStage(string stage)
    {
        Info.Stage = stage;
        Notify();
    }

    public void Increment(int amount = 1)
    {
        Info.WorkDone += amount;

        UpdateSpeedAndEta();

        Notify();
    }

    void UpdateSpeedAndEta()
    {
        var now = DateTime.UtcNow;
        var seconds = (now - _lastUpdate).TotalSeconds;

        if (seconds < 0.5) return;

        var workDiff = Info.WorkDone - _lastWorkDone;

        var speed = workDiff / seconds;

        Info.SpeedPerSecond = speed;

        var remaining = Info.TotalWork - Info.WorkDone;

        if (speed > 0)
            Info.EstimatedRemaining =
                TimeSpan.FromSeconds(remaining / speed);

        _lastWorkDone = Info.WorkDone;
        _lastUpdate = now;
    }

    public void Complete()
    {
        Info.WorkDone = Info.TotalWork;
        Info.Stage = "Completed";
        Notify();
    }

    void Notify() => OnChange?.Invoke();
}