namespace Boilerate.Application.Catalog.Media;

public sealed record MediaDto(Guid Id, string ContentType, long Size, string State, string Version);

public sealed record OpenedMediaDto(Stream Stream, string ContentType);
