namespace Boilerate.Application.Common.Models;

public sealed record Page<T>(List<T> Items, int Total, int PageNumber, int PageSize);