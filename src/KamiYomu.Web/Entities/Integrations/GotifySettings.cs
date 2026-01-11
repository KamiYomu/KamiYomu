namespace KamiYomu.Web.Entities.Integrations;

public class GotifySettings
{
    protected GotifySettings() { }
    public GotifySettings(bool enabled, Uri serviceUri, string apiKey)
    {
        Update(enabled, serviceUri, apiKey);
    }

    public void Update(bool enabled, Uri serviceUri, string apiKey)
    {
        Enabled = enabled;
        ServiceUri = serviceUri;
        ApiKey = apiKey;
    }

    internal void UpdateApiKey(string apiKey)
    {
        ApiKey = apiKey;
    }

    internal void Disable()
    {
        Enabled = false;
    }

    internal void Enable()
    {
        Enabled = true;
    }

    public bool Enabled { get; private set; }
    public Uri ServiceUri { get; private set; }
    public string ApiKey { get; private set; }
}
