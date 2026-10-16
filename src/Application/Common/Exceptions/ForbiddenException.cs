using System.Net;

namespace Boilerate.Application.Common.Exceptions;

/// <summary>
/// Exception khi user không có permission để thực hiện action
/// HTTP Status Code: 403 Forbidden
/// </summary>
public class ForbiddenException : CustomException
{
    /// <summary>
    /// Constructor với message
    /// </summary>
    /// <param name="message">Message mô tả permission nào bị thiếu</param>
    public ForbiddenException(string message)
        : base(message, null, HttpStatusCode.Forbidden)
    {
    }
}
