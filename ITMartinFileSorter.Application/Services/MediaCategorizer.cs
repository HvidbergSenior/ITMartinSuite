using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class MediaCategorizer : IMediaCategorizer
{
    private readonly ImageCategorizer _image;
    private readonly VideoCategorizer _video;
    private readonly AudioCategorizer _audio;
    private readonly DocumentCategorizer _document;

    public MediaCategorizer(
        ImageCategorizer image,
        VideoCategorizer video,
        AudioCategorizer audio,
        DocumentCategorizer document)
    {
        _image = image;
        _video = video;
        _audio = audio;
        _document = document;
    }

    public void Categorize(MediaFile file)
    {
        switch (file.Type)
        {
            case MediaType.Image:
                _image.Categorize(file);
                break;

            case MediaType.Video:
                _video.Categorize(file);
                break;

            case MediaType.Audio:
                _audio.Categorize(file);
                break;

            case MediaType.Document:
                _document.Categorize(file);
                break;
        }
    }
}