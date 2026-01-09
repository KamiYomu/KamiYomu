namespace KamiYomu.Web.Entities;

public class KavitaSettings
{
    protected KavitaSettings() { }

    public KavitaSettings(Uri serviceUri, string username, string password, bool enabled) : this()
    {
        Update(serviceUri, username, password, enabled);
    }

    public Uri? ServiceUri { get; private set; }
    public string? Username { get; private set; }
    public string? Password { get; private set; }
    public bool Enabled { get; private set; }

    public void SetEnableState(bool enabled)
    {
        Enabled = enabled;
    }

    public void UpdatePassword(string password)
    {
        Password = password;
    }

    public void Update(Uri serviceUri, string username, string password, bool enabled)
    {
        ServiceUri = serviceUri;
        Username = username;
        Password = password;
        Enabled = enabled;
    }
}
