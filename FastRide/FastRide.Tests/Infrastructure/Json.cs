using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FastRide.Tests.Infrastructure;

/// <summary>
/// The tests speak the same JSON dialect as the real clients: enums as strings, web naming.
/// If this drifts from the API's configuration, the tests stop testing what ships.
/// </summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public static class HttpExtensions
{
    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json.Options);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, Json.Options);

    /// <summary>Deserialize a successful response, failing the test with the body if it was not successful.</summary>
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected a success status but got {(int)response.StatusCode}. Body: {payload}");

        var value = JsonSerializer.Deserialize<T>(payload, Json.Options);
        Assert.NotNull(value);

        return value;
    }

    /// <summary>Post and deserialize in one step.</summary>
    public static async Task<TResponse> PostAndReadAsync<TRequest, TResponse>(
        this HttpClient client, string url, TRequest body)
    {
        using var response = await client.PostJsonAsync(url, body);
        return await response.ReadAsync<TResponse>();
    }

    public static async Task<TResponse> PutAndReadAsync<TRequest, TResponse>(
        this HttpClient client, string url, TRequest body)
    {
        using var response = await client.PutJsonAsync(url, body);
        return await response.ReadAsync<TResponse>();
    }

    public static async Task<T> GetAndReadAsync<T>(this HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        return await response.ReadAsync<T>();
    }

    /// <summary>Status code of a call whose body we do not care about.</summary>
    public static async Task<int> StatusOfAsync(this Task<HttpResponseMessage> call)
    {
        using var response = await call;
        return (int)response.StatusCode;
    }
}
