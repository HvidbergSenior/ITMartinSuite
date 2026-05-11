using System.Diagnostics;
using ITMartin.Media.Domain.Entities;
using ITMartinFileSorter.Application.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class ProgressService : IProgressService
{
    public ProgressInfo Info { get; private set; } = new();

    public event Action? OnChange;

    private readonly Stopwatch _timer = new();

    private int _lastWorkDone = 0;

    private DateTime _lastUpdate =
        DateTime.UtcNow;

    public void Start(
        string stage,
        int totalWork)
    {
        Info = new ProgressInfo
        {
            Stage = stage,
            TotalWork = totalWork,
            WorkDone = 0,
            SpeedPerSecond = 0,
            EstimatedRemaining = null
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

        ClampProgress();

        UpdateSpeedAndEta();

        Notify();
    }

    // =========================
    // NEW
    // =========================

    public void Update(int value)
    {
        Info.WorkDone = value;

        ClampProgress();

        UpdateSpeedAndEta();

        Notify();
    }

    // =========================
    // COMPLETE
    // =========================

    public void Complete()
    {
        Info.WorkDone =
            Info.TotalWork;

        Info.Stage =
            "Completed";

        Info.EstimatedRemaining =
            TimeSpan.Zero;

        Notify();
    }

    // =========================
    // HELPERS
    // =========================

    private void ClampProgress()
    {
        if (Info.WorkDone < 0)
            Info.WorkDone = 0;

        if (Info.WorkDone > Info.TotalWork)
            Info.WorkDone = Info.TotalWork;
    }

    private void UpdateSpeedAndEta()
    {
        var now = DateTime.UtcNow;

        var seconds =
            (now - _lastUpdate)
            .TotalSeconds;

        if (seconds < 0.5)
            return;

        var workDiff =
            Info.WorkDone - _lastWorkDone;

        var speed =
            workDiff / seconds;

        Info.SpeedPerSecond =
            speed;

        var remaining =
            Info.TotalWork - Info.WorkDone;

        if (speed > 0)
        {
            Info.EstimatedRemaining =
                TimeSpan.FromSeconds(
                    remaining / speed);
        }

        _lastWorkDone =
            Info.WorkDone;

        _lastUpdate =
            now;
    }

    private void Notify()
    {
        OnChange?.Invoke();
    }
}