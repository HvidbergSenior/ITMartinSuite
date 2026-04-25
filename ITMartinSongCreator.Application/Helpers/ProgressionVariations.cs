using System;
using System.Linq;
using System.Collections.Generic;

namespace ITMartinSongCreator.Application.Helpers;

public static class ProgressionVariations
{
    public static List<string> Invert(List<string> chords)
    {
        var list = new List<string>(chords);
        list.Reverse();
        return list;
    }

    public static List<string> DropLast(List<string> chords)
    {
        if (chords.Count <= 1) return chords;
        return chords.Take(chords.Count - 1).ToList();
    }

    public static List<string> AddPassingChord(List<string> chords, string passingChord)
    {
        var result = new List<string>();
        foreach (var chord in chords)
        {
            result.Add(chord);
            result.Add(passingChord);
        }
        return result;
    }

    public static List<string> LiftChords(List<string> chords, int semitones)
    {
        return ChordTransposer.Transpose(chords, semitones);
    }
}