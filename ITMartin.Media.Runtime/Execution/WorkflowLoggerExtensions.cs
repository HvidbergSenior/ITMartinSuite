using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Runtime.Execution;

public static class WorkflowLoggerExtensions
{
    public static void LogWorkflowStepStart(
        this ILogger logger,
        string stepName,
        int stepIndex,
        int totalSteps)
    {
        logger.LogInformation(
            """
            ==================================================
            [{StepIndex}/{TotalSteps}] START {Step}
            ==================================================
            """,
            stepIndex,
            totalSteps,
            stepName);
    }

    public static void LogWorkflowStepEnd(
        this ILogger logger,
        string stepName,
        int stepIndex,
        int totalSteps,
        TimeSpan elapsed)
    {
        logger.LogInformation(
            """
            ==================================================
            [{StepIndex}/{TotalSteps}] COMPLETE {Step}
            Duration: {Elapsed}
            ==================================================
            """,
            stepIndex,
            totalSteps,
            stepName,
            elapsed);
    }

    public static void LogProgress(
        this ILogger logger,
        string step,
        int current,
        int total,
        string message = "")
    {
        var percent =
            total == 0
                ? 0
                : (double)current / total * 100;

        logger.LogInformation(
            "[{Step}] {Current}/{Total} ({Percent:F1}%) {Message}",
            step,
            current,
            total,
            percent,
            message);
    }
}