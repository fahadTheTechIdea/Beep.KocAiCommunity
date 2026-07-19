namespace Beep.KocAiCommunity.Contracts.Datasets;

public sealed record DatasetVersionDto(
    int VersionNumber, string Status, long TotalSizeBytes, string? Sha256, string? Notes, DateTime? PublishedUtc, DateTime CreatedUtc);

public sealed record DatasetFileDto(Guid Id, string LogicalPath, string ContentType, long SizeBytes, long RowCount);

public sealed record DatasetSchemaColumnDto(int Ordinal, string ColumnName, string DataType, bool Nullable);

public sealed record DatasetProfileColumnDto(
    string ColumnName, long NullCount, long DistinctCount, double? Min, double? Max, double? Mean);

public sealed record DatasetProfileDto(long SampledRows, long TotalRows, DateTime GeneratedUtc, IReadOnlyList<DatasetProfileColumnDto> Columns);

public sealed record DatasetVersionDetailDto(
    DatasetVersionDto Version,
    IReadOnlyList<DatasetFileDto> Files,
    IReadOnlyList<DatasetSchemaColumnDto> Schema,
    DatasetProfileDto? Profile);

public sealed record ImportUrlRequest(string Url);
