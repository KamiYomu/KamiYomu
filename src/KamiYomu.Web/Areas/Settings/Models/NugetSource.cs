namespace KamiYomu.Web.Areas.Settings.Models;

public class NugetSource
{
    protected NugetSource() { }
    public NugetSource(string displayName, Uri url, string? userName, string? password)
    {
        DisplayName = displayName;
        Url = url;
        UserName = userName;
        Password = password;
    }

    public Guid Id { get; private set; }
    public string DisplayName { get; private set; }
    public Uri Url { get; private set; }
    public string? UserName { get; private set; }
    public string? Password { get; private set; }

    internal void Update(string displayName, Uri uri, string username, string password)
    {
        DisplayName = displayName;
        Url = uri;
        UserName = username;
        Password = password;
    }
}
