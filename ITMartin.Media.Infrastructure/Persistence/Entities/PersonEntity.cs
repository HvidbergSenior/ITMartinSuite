namespace ITMartin.Media.Infrastructure.Persistence.Entities;

public sealed class PersonEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    // The year this person was born, when known. Used as a hard rule, not a
    // hint: a photo taken before it cannot show them, whatever the face
    // matcher says.
    //
    // It exists because face recognition on young children barely works. On
    // 2026-09-11 Theodor - about seven in 2020 - was matched to 6,144 photos,
    // 834 of them Olympus shots from 2006-2010, years before he existed. His
    // toddler-age reference photos matched every small child in the library,
    // and tightening the similarity threshold from 0.45 to 0.65 removed almost
    // none of them (834 -> 790): toddler faces are simply too alike for the
    // embedding to separate. A birth year removes every impossible match at
    // zero cost and needs nothing from the matcher at all.
    public int? BornYear { get; set; }

    // Per-person similarity floor for the folder, when the default (0.45) is
    // too generous for this face. Eigil at 0.45 had 318 matches below 0.55
    // and the owner found him in 4 of the weakest 48 (2026-09-14); Vibeke
    // needed 0.55 too. It lives here so a rebuild of all folders keeps it -
    // a threshold passed on one call was lost on the next rebuild.
    public double? MatchThreshold { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
