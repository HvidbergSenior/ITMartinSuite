using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class MediaSourceProcessor
{
    public MediaSource Get(
        MediaFile file)
    {
        return file.Source;
    }

    public void Set(
        MediaFile file,
        MediaSource source)
    {
        file.Source =
            source;
    }

    public bool IsWhatsApp(
        MediaFile file)
    {
        return file.Source ==
               MediaSource.WhatsApp;
    }

    public bool IsTelegram(
        MediaFile file)
    {
        return file.Source ==
               MediaSource.Telegram;
    }

    public bool IsDownload(
        MediaFile file)
    {
        return file.Source ==
               MediaSource.Download;
    }
}