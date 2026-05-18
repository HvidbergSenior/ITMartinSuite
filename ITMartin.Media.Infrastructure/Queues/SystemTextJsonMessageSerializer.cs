// File: ITMartin.Media.Infrastructure/Queues/SystemTextJsonMessageSerializer.cs

using System.Text.Json;
using ITMartin.Media.Application.Abstractions.Queues;

namespace ITMartin.Media.Infrastructure.Queues;

public sealed class SystemTextJsonMessageSerializer
    : IMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Serialize<T>(T message)
    {
        return JsonSerializer.Serialize(message, Options);
    }

    public T Deserialize<T>(string payload)
    {
        return JsonSerializer.Deserialize<T>(payload, Options)!;
    }
}