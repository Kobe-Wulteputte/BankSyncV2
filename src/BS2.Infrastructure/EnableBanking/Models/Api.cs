using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BS2.Infrastructure.EnableBanking.Models;

/// <summary>
/// Wire format shared by every request and response. Properties are annotated explicitly like V1;
/// the snake_case policy and case-insensitivity only cover anything that slipped through.
/// Nulls are omitted on write: several request models reuse fat response types.
/// </summary>
public static class EnableBankingJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public class ApiError
{
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("detail")] public object? Detail { get; set; }
}

public class ApiResponse<T>
{
    public T? Data { get; set; }
    public ApiError? Error { get; set; }
    public HttpStatusCode? StatusCode { get; set; }
}
