using EasyPass.Domain;

namespace EasyPass.Application;

public interface IStedStore
{
    IReadOnlyList<Sted> Alle();
    Sted? Hent(string id);
    void Gem(Sted sted);
    void GemMange(IEnumerable<Sted> steder);
    bool Slet(string id);
}

public interface ITilbagemeldingStore
{
    IReadOnlyList<Tilbagemelding> Alle();
    void Gem(Tilbagemelding t);
}
