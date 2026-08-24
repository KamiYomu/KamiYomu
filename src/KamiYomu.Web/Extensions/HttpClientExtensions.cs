namespace KamiYomu.Web.Extensions;
/// <summary>
/// httpclient extensions
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Loads the specified header into the HttpRequestMessage headers.
    /// </summary>
    /// <param name="request">The HttpRequestMessage to load the header into.</param>
    /// <param name="headers">The headers to load.</param>
    public static void LoadHttpRequestHeaders(this HttpRequestMessage request, IEnumerable<KeyValuePair<string, string>> headers)
    {
        foreach (KeyValuePair<string, string> header in headers)
        {
            _ = request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    /// <summary>
    /// Loads the specified header into the HttpClient default request headers.
    /// </summary>
    /// <param name="httpClient">The HttpClient to load the header into.</param>
    /// <param name="headers">The headers to load.</param>
    public static void LoadHttpRequestHeaders(this HttpClient httpClient, IEnumerable<KeyValuePair<string, string>> headers)
    {
        foreach (KeyValuePair<string, string> header in headers)
        {
            _ = httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}
