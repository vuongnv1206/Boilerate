using System.Net;

namespace Boilerate.Application.Common.Exceptions;

/// <summary>
/// Exception khi có conflict (duplicate entity, business rule violation, etc.)
/// HTTP Status Code: 409 Conflict
/// </summary>
public class ConflictException : CustomException
{
    /// <summary>
    /// Constructor với message
    /// </summary>
    /// <param name="message">Message mô tả conflict gì</param>
    public ConflictException(string message)
        : base(message, null, HttpStatusCode.Conflict)
    {
    }
}
