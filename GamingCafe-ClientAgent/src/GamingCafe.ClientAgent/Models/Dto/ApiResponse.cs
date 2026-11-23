using System.Net;

namespace GamingCafe.ClientAgent.Models.Dto;

/// <summary>
/// Generic wrapper representing an API result with HTTP status, data, and potential error details.
/// </summary>
public record ApiResponse<T>
{
    public bool IsSuccess { get; init; }
    public HttpStatusCode StatusCode { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsNetworkOrServerUnavailable { get; init; }

    public static ApiResponse<T> Success(T data, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new() { IsSuccess = true, StatusCode = statusCode, Data = data };

    public static ApiResponse<T> Failure(string errorMessage, HttpStatusCode statusCode = HttpStatusCode.BadRequest, bool isNetworkError = false) =>
        new() { IsSuccess = false, StatusCode = statusCode, ErrorMessage = errorMessage, IsNetworkOrServerUnavailable = isNetworkError };
}
