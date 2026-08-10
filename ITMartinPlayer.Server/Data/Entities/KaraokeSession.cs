namespace ITMartinPlayer.Server.Data.Entities;

// A karaoke night - one code the host shows on the TV/Stage view, everyone
// else joins by typing it into their own phone at /join. Deliberately no
// per-participant login/account - just a name typed in, kept in the
// browser's own localStorage so rejoining after a page refresh is seamless.
public class KaraokeSession
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
