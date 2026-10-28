using ITMartinUpload.Application;
using ITMartinUpload.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinUpload.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The disk, json, web push and ImageSharp adapters behind the ports, plus the use cases.</summary>
    public static IServiceCollection AddUpload(this IServiceCollection services)
    {
        services.AddSingleton<UploadStore>();
        services.AddSingleton<IUploadFiles>(sp => sp.GetRequiredService<UploadStore>());
        services.AddSingleton<IMessageStore, MessageStore>();
        services.AddSingleton<ISortReports, SortReportReader>();
        services.AddSingleton<IPushSender, UploadPushService>();
        services.AddSingleton<IThumbnails, ThumbnailFactory>();
        services.AddSingleton<UploadActivity>();
        services.AddSingleton<CustomerFiles>();
        services.AddSingleton<Conversations>();
        return services;
    }
}
