using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class ExportStructureProcessor
{
    private readonly YearFolderProcessor
        _yearFolderProcessor;

    private readonly MonthFolderProcessor
        _monthFolderProcessor;

    public ExportStructureProcessor(
        YearFolderProcessor
            yearFolderProcessor,

        MonthFolderProcessor
            monthFolderProcessor)
    {
        _yearFolderProcessor =
            yearFolderProcessor;

        _monthFolderProcessor =
            monthFolderProcessor;
    }

    public string Build(
        MediaFile file,
        string root)
    {
        var year =
            _yearFolderProcessor
                .Build(file);

        var month =
            _monthFolderProcessor
                .Build(file);

        return Path.Combine(
            root,
            year,
            month);
    }
}