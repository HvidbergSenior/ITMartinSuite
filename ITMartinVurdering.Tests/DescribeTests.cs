using FluentAssertions;
using ITMartinVarer.Contracts;
using ITMartinVurdering.Application;

namespace ITMartinVurdering.Tests;

// Ting/Strik descriptions (2026-11-10): one round = at most ONE AI call; every waiting thing goes back to Lager, also without an answer.
public class DescribeTests
{
    private sealed class FakeLager(List<VurderingDescribe> waiting) : ILagerReview
    {
        public readonly Dictionary<int, VurderingDescription> Kept = [];
        public Task<VurderingQueue?> QueueAsync(string pile, int take, IReadOnlyCollection<int> skip, string usage, CancellationToken ct) => Task.FromResult<VurderingQueue?>(null);
        public Task<VurderingQueue?> SearchAsync(string text, IReadOnlyCollection<int> skip, CancellationToken ct) => Task.FromResult<VurderingQueue?>(null);
        public Task<string?> SetPhotoHiddenAsync(int itemId, bool hidden, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> CoverMarkAsync(int itemId, PhotoArea area, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> UncoverAsync(int itemId, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> DiscardAsync(int itemId, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> RotateAsync(int itemId, int degrees, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> CropOwnAsync(int itemId, PhotoArea keep, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> UndoDiscardAsync(int itemId, string was, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> RecutAsync(int itemId, PhotoArea area, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<byte[]?> PileAsync(int itemId, CancellationToken ct) => Task.FromResult<byte[]?>(null);
        public Task<string?> SetUsageAsync(int itemId, string usage, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> SetPriceAsync(int itemId, int kr, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<byte[]?> PhotoAsync(int itemId, CancellationToken ct) => Task.FromResult<byte[]?>(itemId == 3 ? null : [1, 2]);
        public Task<string?> SetRareAsync(int itemId, bool rare, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<string?> SetNoteAsync(int itemId, string note, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task<int?> FindPricesAsync(int itemId, CancellationToken ct) => Task.FromResult<int?>(null);
        public Task<List<VurderingDescribe>?> WaitingDescriptionsAsync(int take, CancellationToken ct) => Task.FromResult<List<VurderingDescribe>?>(waiting.Take(take).ToList());
        public Task<bool> SetDescriptionAsync(int itemId, VurderingDescription d, CancellationToken ct) { Kept[itemId] = d; return Task.FromResult(true); }
    }

    private sealed class OneCallValuer : IThingValuer
    {
        public int Calls, Left = 5;
        public bool Enabled => true;
        public int CallsLeftToday() => Left;
        public Task<List<ThingValue>> ValueAsync(IReadOnlyList<ThingPhoto> photos, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(photos.Where(p => p.Index == 0).Select(p =>
                new ThingValue(p.Index, "Safaristol i læder", "Se efter stempel", 1500, 4000, "spørg en ekspert", "Lauritz.com", "Kendt type",
                    "safaristol læder", "Safaristolen blev tegnet i 1930'erne ...")).ToList());
        }
    }

    [Test]
    public async Task One_round_is_one_call_and_every_thing_goes_back()
    {
        var lager = new FakeLager([new(1, "Ting", "Stol"), new(2, "Ting", "Kabinet"), new(3, "Ting", "Uden billede")]);
        var ai = new OneCallValuer();
        (await new DescribeUseCases(lager, ai).RoundAsync()).Should().Be(3);
        ai.Calls.Should().Be(1);
        lager.Kept[1].Should().Be(new VurderingDescription("Safaristol i læder", "Safaristolen blev tegnet i 1930'erne ...", 1500, 4000));
        lager.Kept[2].Title.Should().BeEmpty("no answer for it - Lager only notes it was looked at");
        lager.Kept[3].Text.Should().BeEmpty("no photo");
    }

    [Test]
    public async Task No_calls_left_today_means_no_call()
    {
        var ai = new OneCallValuer { Left = 0 };
        (await new DescribeUseCases(new FakeLager([new(1, "Ting", "Stol")]), ai).RoundAsync()).Should().Be(0);
        ai.Calls.Should().Be(0);
    }
}
