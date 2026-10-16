using System.Net;

namespace Boilerate.Application.Common.Exceptions;

/// <summary>
/// Base exception class cho tất cả custom exceptions trong application
/// Kế thừa từ Exception để có đầy đủ properties (Message, StackTrace, etc.)
/// </summary>
public class CustomException : Exception
{
    /// <summary>
    /// Danh sách error messages (cho validation hoặc multiple errors)
    /// </summary>
    public List<string>? ErrorMessages { get; }

    /// <summary>
    /// HTTP status code tương ứng với exception này
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Constructor với message, errors, và status code
    /// </summary>
    /// <param name="message">Exception message chính</param>
    /// <param name="errors">Danh sách error messages (optional)</param>
    /// <param name="statusCode">HTTP status code (default: 500)</param>
    /// <param name="innerException">Original failure for server diagnostics.</param>
    public CustomException(
        string message,
        List<string>? errors = default,
        HttpStatusCode statusCode = HttpStatusCode.InternalServerError,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorMessages = errors;
        StatusCode = statusCode;
    }
}
