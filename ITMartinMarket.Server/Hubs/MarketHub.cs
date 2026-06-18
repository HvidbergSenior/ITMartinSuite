using Microsoft.AspNetCore.SignalR;

namespace ITMartinMarket.Server.Hubs;

public sealed class MarketHub : Hub
{
    public async Task ItemPosted(object item) =>
        await Clients.Others.SendAsync("ItemPosted", item);

    public async Task BidPlaced(Guid itemId, string buyerName, decimal? amount) =>
        await Clients.Others.SendAsync("BidPlaced", itemId, buyerName, amount);

    public async Task MessageSent(Guid itemId, string senderName, string text) =>
        await Clients.Others.SendAsync("MessageSent", itemId, senderName, text);

    public async Task ItemSold(Guid itemId, string soldTo) =>
        await Clients.Others.SendAsync("ItemSold", itemId, soldTo);
}
