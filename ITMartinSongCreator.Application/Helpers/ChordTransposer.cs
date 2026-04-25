namespace ITMartinSongCreator.Application.Helpers;

public static class ChordTransposer
{
    private static readonly string[] Notes = 
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static List<string> Transpose(List<string> chords, int semitones)
    {
        var result = new List<string>();

        foreach (var chord in chords)
        {
            // Extract base note and optional suffix (e.g., Em7 → E + m7)
            var note = chord.Length > 1 && (chord[1] == '#' || chord[1] == 'b') 
                ? chord.Substring(0, 2) 
                : chord.Substring(0, 1);

            var suffix = chord.Substring(note.Length);

            // Handle flats (convert to sharps)
            var normalizedNote = note switch
            {
                "Db" => "C#",
                "Eb" => "D#",
                "Gb" => "F#",
                "Ab" => "G#",
                "Bb" => "A#",
                _ => note
            };

            var index = Array.IndexOf(Notes, normalizedNote);
            if (index == -1)
            {
                result.Add(chord); // unknown chord, leave as is
                continue;
            }

            var newIndex = (index + semitones) % Notes.Length;
            if (newIndex < 0) newIndex += Notes.Length;

            result.Add(Notes[newIndex] + suffix);
        }

        return result;
    }
}