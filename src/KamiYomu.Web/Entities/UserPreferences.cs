using System.Globalization;

namespace KamiYomu.Web.Entities;

public class UserPreference
{
    protected UserPreference() { }
    public UserPreference(CultureInfo culture)
    {
        SetCulture(culture);
    }

    public CultureInfo GetCulture()
    {
        return CultureInfo.GetCultureInfo(LanguageId);
    }

    internal void SetCulture(CultureInfo culture)
    {
        Language = culture.Name;
        LanguageId = culture.LCID;
    }

    internal void SetFamilySafeMode(bool familySafeMode)
    {
        FamilySafeMode = familySafeMode;
    }

    internal void SetFilePathTemplate(string filePathTemplate)
    {
        FilePathTemplate = filePathTemplate;
    }

    internal void SetComicInfoTitleTemplate(string comicInfoTitleTemplateFormat)
    {
        ComicInfoTitleTemplate = comicInfoTitleTemplateFormat;
    }

    internal void SetComicInfoSeriesTemplate(string comicInfoSeriesTemplate)
    {
        ComicInfoSeriesTemplate = comicInfoSeriesTemplate;
    }

    public Guid Id { get; private set; }
    public string Language { get; private set; }
    public int LanguageId { get; private set; }
    public bool FamilySafeMode { get; private set; } = true;
    public string FilePathTemplate { get; private set; }
    public string ComicInfoTitleTemplate { get; private set; }
    public string ComicInfoSeriesTemplate { get; private set; }
}
