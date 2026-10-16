using System.Net;

namespace Boilerate.Application.Common.Exceptions;

/// <summary>
/// Exception khi user chưa authenticate (chưa login)
/// HTTP Status Code: 401 Unauthorized
/// </summary>
public class UnauthorizedException : CustomException
{
    /// <summary>
    /// Constructor với message
    /// </summary>
    /// <param name="message">Message yêu cầu user login</param>
    public UnauthorizedException(string message)
        : base(message, null, HttpStatusCode.Unauthorized)
    {
    }
}
