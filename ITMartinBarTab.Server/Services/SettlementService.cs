using ITMartinBarTab.Server.Data.Entities;

namespace ITMartinBarTab.Server.Services;

public sealed record ParticipantBalance(Guid Id, string Name, string Color, decimal Paid, decimal Owed, decimal Net);
public sealed record Transfer(string From, string FromColor, string To, string ToColor, decimal Amount);

public sealed class SettlementService
{
    public (List<ParticipantBalance> Balances, List<Transfer> Transfers) Calculate(Session session)
    {
        var balances = new Dictionary<Guid, (decimal paid, decimal owed)>();

        foreach (var p in session.Participants)
            balances[p.Id] = (0, 0);

        foreach (var drink in session.Drinks)
        {
            // who paid
            var payer = drink.AddedByParticipantId;
            if (balances.ContainsKey(payer))
                balances[payer] = (balances[payer].paid + drink.Price, balances[payer].owed);

            // who consumed
            if (drink.IsRound)
            {
                var inRound = drink.Shares
                    .Where(s => s.Share != ShareType.None)
                    .ToList();

                if (inRound.Count > 0)
                {
                    var each = drink.Price / inRound.Count;
                    foreach (var share in inRound)
                    {
                        if (balances.ContainsKey(share.ParticipantId))
                            balances[share.ParticipantId] = (balances[share.ParticipantId].paid, balances[share.ParticipantId].owed + each);
                    }
                }
            }
            else
            {
                // Sip = flat 5% of drink price per person, remainder split among Full/Half by weight
                const decimal SipRate = 0.05m;

                var sipShares  = drink.Shares.Where(s => s.Share == ShareType.Taste).ToList();
                var mainShares = drink.Shares.Where(s => s.Share is ShareType.Full or ShareType.Half).ToList();

                decimal perSip    = Math.Round(drink.Price * SipRate, 2);
                decimal totalSips = sipShares.Count * perSip;
                decimal remainder = drink.Price - totalSips;

                foreach (var share in sipShares)
                    if (balances.ContainsKey(share.ParticipantId))
                        balances[share.ParticipantId] = (balances[share.ParticipantId].paid, balances[share.ParticipantId].owed + perSip);

                if (mainShares.Count > 0)
                {
                    var totalWeight = mainShares.Sum(s => DrinkShare.Weight(s.Share));
                    foreach (var share in mainShares)
                    {
                        var portion = (decimal)(DrinkShare.Weight(share.Share) / totalWeight) * remainder;
                        if (balances.ContainsKey(share.ParticipantId))
                            balances[share.ParticipantId] = (balances[share.ParticipantId].paid, balances[share.ParticipantId].owed + portion);
                    }
                }
                else if (sipShares.Count > 0)
                {
                    // Nobody had Full/Half — split the remainder equally among sippers
                    var extra = remainder / sipShares.Count;
                    foreach (var share in sipShares)
                        if (balances.ContainsKey(share.ParticipantId))
                            balances[share.ParticipantId] = (balances[share.ParticipantId].paid, balances[share.ParticipantId].owed + extra);
                }
            }
        }

        var participantMap = session.Participants.ToDictionary(p => p.Id);
        var result = balances.Select(kv =>
        {
            var p = participantMap[kv.Key];
            return new ParticipantBalance(kv.Key, p.Name, p.Color, kv.Value.paid, kv.Value.owed, kv.Value.paid - kv.Value.owed);
        }).ToList();

        var transfers = Simplify(result, participantMap);
        return (result, transfers);
    }

    private static List<Transfer> Simplify(List<ParticipantBalance> balances, Dictionary<Guid, Participant> map)
    {
        var creditors = new Queue<(Guid id, decimal amount)>(
            balances.Where(b => b.Net > 0.01m)
                    .OrderByDescending(b => b.Net)
                    .Select(b => (b.Id, b.Net)));
        var debtors = new Queue<(Guid id, decimal amount)>(
            balances.Where(b => b.Net < -0.01m)
                    .OrderByDescending(b => -b.Net)
                    .Select(b => (b.Id, -b.Net)));

        var transfers = new List<Transfer>();

        while (creditors.Count > 0 && debtors.Count > 0)
        {
            var (credId, credAmt) = creditors.Dequeue();
            var (debtId, debtAmt) = debtors.Dequeue();

            var amount = Math.Min(credAmt, debtAmt);
            transfers.Add(new Transfer(
                map[debtId].Name, map[debtId].Color,
                map[credId].Name, map[credId].Color,
                Math.Round(amount, 2)));

            if (credAmt - amount > 0.01m) creditors.Enqueue((credId, credAmt - amount));
            if (debtAmt - amount > 0.01m) debtors.Enqueue((debtId, debtAmt - amount));
        }

        return transfers;
    }
}
