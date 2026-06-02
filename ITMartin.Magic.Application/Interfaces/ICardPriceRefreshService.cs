namespace ITMartin.Magic.Application.Interfaces;

public interface ICardPriceRefreshService
{
    Task RefreshAsync(
        CancellationToken cancellationToken = default);
}