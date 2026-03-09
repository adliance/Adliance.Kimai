using System.Net;

namespace Adliance.Kimai.Client.Exceptions;

/// <summary>
/// Represents errors that occur because of an invalid request.
/// </summary>
/// <param name="statusCode">The HTTP status code of the failed request.</param>
/// <param name="responseBody">The response body of the failed request.</param>
/// <param name="requestUri">The URI of the request.</param>
/// <param name="message">The message that describes the error.</param>
public class ApiException(HttpStatusCode statusCode, string responseBody, string requestUri, string? message = null) : Exception(message)
{
    /// <summary>
    /// The HTTP status code of the failed request.
    /// </summary>
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>
    /// The response body of the failed request.
    /// </summary>
    public string ResponseBody { get; } = responseBody;

    /// <summary>
    /// The URI of the HTTP request that caused the exception.
    /// </summary>
    public string RequestUri { get; } = requestUri;
}
