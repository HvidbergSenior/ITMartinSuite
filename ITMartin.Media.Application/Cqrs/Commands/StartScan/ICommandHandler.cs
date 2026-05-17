namespace ITMartin.Media.Application.CQRS.Commands.StartScan;

public interface ICommandHandler<in TCommand>
{
    Task HandleAsync(
        TCommand command,
        CancellationToken cancellationToken);
}