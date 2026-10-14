using ITMartinR6Assistant.Domain;

namespace ITMartinR6Assistant.Application;

// Turns what the R6Replays script sends (one r6-dissect JSON per round) into a ReplayMatch.
// Map and site names are matched against the app's own data, because the tool only knows map ids for new seasons.
public interface IReplayParser
{
    ReplayMatch? Parse(string json, R6GameData data);
}
