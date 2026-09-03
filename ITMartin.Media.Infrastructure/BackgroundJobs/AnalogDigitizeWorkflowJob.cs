using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.AnalogDigitize;

namespace ITMartin.Media.Infrastructure.BackgroundJobs;

public sealed class AnalogDigitizeWorkflowJob
{
    public required StartAnalogDigitizeRequest
        Request { get; init; }
}