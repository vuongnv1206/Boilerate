using System.Net;

namespace Boilerate.Domain.Common.Exceptions;

public class DomainException : Exception
{
    public List<string>? ErrorMessages { get; }

    public HttpStatusCode StatusCode { get; }

    public DomainException(string message, List<string>? errors = default, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base(message)
    {
        ErrorMessages = errors;
        StatusCode = statusCode;
    }
}
