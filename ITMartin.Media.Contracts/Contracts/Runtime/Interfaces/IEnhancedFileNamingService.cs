using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IEnhancedFileNamingService
{
    string BuildFileName(
        EnhancedMediaItem item);
}