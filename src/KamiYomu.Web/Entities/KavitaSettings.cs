namespace KamiYomu.Web.Entities;

public class KavitaSettings
{
    protected KavitaSettings() { }

    public KavitaSettings(Uri serviceUri, string username, string password, string apiKey, bool enabled) : this()
    {
        Update(serviceUri, username, password, apiKey, enabled);
    }

    public Uri? ServiceUri { get; private set; }
    public string? Username { get; private set; }
    public string? Password { get; private set; }
    public string? ApiKey { get; private set; }
    public bool Enabled { get; private set; }
    public bool IsApiKey()
    {
        return !string.IsNullOrWhiteSpace(ApiKey);
    }

    public void SetEnableState(bool enabled)
    {
        Enabled = enabled;
    }

    public void UpdatePassword(string password)
    {
        Password = password;
    }

    public void UpdateApiKey(string apiKey)
    {
        ApiKey = apiKey;
    }

    public void Update(Uri serviceUri, string username, string password, string apiKey, bool enabled)
    {
        ServiceUri = serviceUri;
        Username = username;
        Password = password;
        ApiKey = apiKey;
        Enabled = enabled;
    }
}
