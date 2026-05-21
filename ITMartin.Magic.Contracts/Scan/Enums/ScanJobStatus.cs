namespace ITMartin.Magic.Contracts.Scan.Enums;

public enum ScanJobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    RetryPending = 4
}