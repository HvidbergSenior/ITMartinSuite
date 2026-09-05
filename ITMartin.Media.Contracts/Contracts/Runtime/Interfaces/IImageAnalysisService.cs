using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IImageAnalysisService
{
    Task<AiAnalysisResult> AnalyzeImageAsync(
        string filePath);

    // Sends every path in one Claude call (one system prompt, one set of
    // fixed tokens, N images) instead of N separate calls - see CLAUDE.md
    // "AI/Claude API cost discipline": a per-file call loop is the single
    // most expensive mistake to make on a library that can run into the tens
    // of thousands of files. Results come back in the same order as
    // filePaths; a path that fails to analyze (unreadable, no verdict
    // returned) gets an Empty()-equivalent result rather than being omitted,
    // so callers can always zip filePaths with the result list 1:1.
    Task<IReadOnlyList<AiAnalysisResult>> AnalyzeImagesBatchAsync(
        IReadOnlyList<string> filePaths);
}