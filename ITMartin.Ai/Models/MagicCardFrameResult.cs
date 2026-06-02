using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Ai.Models;

public sealed record MagicCardFrameResult(
    MagicCardFrameType FrameType,
    bool IsOldBorder,
    bool IsWhiteBorder);