// File: ITMartin.Media.Application/Abstractions/Queues/IMessageSerializer.cs

namespace ITMartin.Media.Application.Abstractions.Queues;

public interface IMessageSerializer
{
    string Serialize<T>(T message);

    T Deserialize<T>(string payload);
}