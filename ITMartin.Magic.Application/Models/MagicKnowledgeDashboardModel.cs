using ITMartin.Magic.Domain.Entities;

namespace ITMartin.Magic.Application.Models;

public sealed class MagicKnowledgeDashboardModel
{
    public int TotalSets { get; set; }

    public int KnownSymbols { get; set; }

    public int MissingSymbols { get; set; }

    public decimal CoveragePercent { get; set; }

    public List<MagicSetKnowledge> MissingKnowledge { get; set; } = [];

    public List<MagicSetKnowledge> KnownKnowledge { get; set; } = [];
}