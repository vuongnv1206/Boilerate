using System.Net;

namespace Boilerate.Application.Common.Exceptions;

/// <summary>
/// Exception cho internal server errors hoặc unexpected errors
/// HTTP Status Code: 500 Internal Server Error
/// </summary>
public class InternalServerException : CustomException
{
    /// <summary>
    /// Constructor với message và optional error list
    /// </summary>
    /// <param name="message">Error message chính</param>
    /// <param name="errors">Danh sách detailed errors (optional)</param>
    /// <param name="innerException">Original failure preserved for server diagnostics.</param>
    public InternalServerException(string message, List<string>? errors = default, Exception? innerException = null)
        : base(message, errors, HttpStatusCode.InternalServerError, innerException)
    {
    }
}
