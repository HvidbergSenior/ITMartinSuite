using ITMartinKunder.Client;
using ITMartinKunder.Contracts;

namespace ITMartinSalg.Infrastructure.External;

// A paid order -> the buyer is a customer in Kunder (the Kundekartotek for all apps, 2026-10-21). As a buyer only:
// it is not a yes to offers. What Kunder does not take now waits in data/kunder-outbox.
internal sealed class KunderBuyers(KunderClient kunder) : ICustomerRecord
{
    public Task BoughtAsync(Order order, CancellationToken ct) =>
        order.Email == "" ? Task.CompletedTask
            : kunder.BoughtAsync(new PurchaseRequest(order.Email, order.Name, order.Phone, "Bogshoppen Salg", order.Token,
                order.Line, order.PaidKr, order.PaidAt ?? DateTime.UtcNow), ct);
}
