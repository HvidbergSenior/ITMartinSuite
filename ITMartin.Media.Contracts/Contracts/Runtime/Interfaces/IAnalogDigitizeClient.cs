using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.AnalogDigitize;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IAnalogDigitizeClient
{
    Task<Guid> StartAsync(
        StartAnalogDigitizeRequest request,
        CancellationToken cancellationToken);
}