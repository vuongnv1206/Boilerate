using System.Net;

namespace Boilerate.Application.Common.Exceptions;

/// <summary>
/// Exception khi không tìm thấy entity/resource
/// HTTP Status Code: 404 Not Found
/// </summary>
public class NotFoundException : CustomException
{
    /// <summary>
    /// Constructor với message
    /// </summary>
    /// <param name="message">Message mô tả entity nào không tìm thấy</param>
    public NotFoundException(string message)
        : base(message, null, HttpStatusCode.NotFound)
    {
    }
}
