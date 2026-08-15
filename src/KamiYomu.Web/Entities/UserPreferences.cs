using System.Globalization;

using KamiYomu.Web.Entities.Integrations;

namespace KamiYomu.Web.Entities;

/// <summary>
/// User preferences entity class that stores user-specific settings and preferences for the application.
/// </summary>
public class UserPreference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserPreference"/> class with default values.
    /// </summary>
    protected UserPreference() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPreference"/> class with the specified culture.
    /// </summary>
    /// <param name="culture">The culture information to set for the user's language preferences.</param>
    public UserPreference(CultureInfo culture)
    {
        SetCulture(culture);
    }

    /// <summary>
    /// Gets the culture information associated with the user's language preferences.
    /// </summary>
    /// <returns>A <see cref="CultureInfo"/> object representing the user's language settings.</returns>
    public CultureInfo GetCulture()
    {
        return CultureInfo.GetCultureInfo(LanguageId);
    }

    /// <summary>
    /// Sets the user's language preferences based on the specified culture information.
    /// </summary>
    /// <param name="culture">The culture information to apply to the user's preferences.</param>
    public void SetCulture(CultureInfo culture)
    {
        Language = culture.Name;
        LanguageId = culture.LCID;
    }

    /// <summary>
    /// Sets the family safe mode preference for the user.
    /// </summary>
    /// <param name="familySafeMode">A value indicating whether family safe mode should be enabled.</param>
    public void SetFamilySafeMode(bool familySafeMode)
    {
        FamilySafeMode = familySafeMode;
    }

    /// <summary>
    /// Sets the file path template used for organizing downloaded files.
    /// </summary>
    /// <param name="filePathTemplate">The template string defining the file path structure.</param>
    public void SetFilePathTemplate(string filePathTemplate)
    {
        FilePathTemplate = filePathTemplate;
    }

    /// <summary>
    /// Sets the ComicInfo title template format for metadata generation.
    /// </summary>
    /// <param name="comicInfoTitleTemplateFormat">The template string for ComicInfo title formatting.</param>
    public void SetComicInfoTitleTemplate(string comicInfoTitleTemplateFormat)
    {
        ComicInfoTitleTemplate = comicInfoTitleTemplateFormat;
    }

    /// <summary>
    /// Sets the ComicInfo series template format for metadata generation.
    /// </summary>
    /// <param name="comicInfoSeriesTemplate">The template string for ComicInfo series formatting.</param>
    public void SetComicInfoSeriesTemplate(string comicInfoSeriesTemplate)
    {
        ComicInfoSeriesTemplate = comicInfoSeriesTemplate;
    }

    /// <summary>
    /// Sets the daily execution time for the crawler cron job.
    /// </summary>
    /// <param name="dailyExecutionTime">The time of day when the crawler should execute, or <see langword="null"/> to disable scheduled execution.</param>
    public void SetDailyExecutionTime(TimeSpan? dailyExecutionTime)
    {
        DailyExecutionTime = dailyExecutionTime;
    }

    /// <summary>
    /// Sets the Kavita integration settings for the user.
    /// </summary>
    /// <param name="settings">The Kavita settings configuration to apply.</param>
    public void SetKavitaSettings(KavitaSettings settings)
    {
        KavitaSettings = settings;
    }

    /// <summary>
    /// Sets the Gotify notification integration settings for the user.
    /// </summary>
    /// <param name="settings">The Gotify settings configuration to apply.</param>
    public void SetGotifySettings(GotifySettings settings)
    {
        GotifySettings = settings;
    }

    /// <summary>
    /// Gets the unique identifier for the user preference record.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the language name in the format "language-region" (e.g., "en-US", "ja-JP").
    /// </summary>
    public string Language { get; private set; }

    /// <summary>
    /// Gets the locale identifier (LCID) for the user's language preference.
    /// </summary>
    public int LanguageId { get; private set; }

    /// <summary>
    /// Gets a value indicating whether family safe mode is enabled for content filtering.
    /// </summary>
    public bool FamilySafeMode { get; private set; } = true;

    /// <summary>
    /// Gets the file path template used for organizing downloaded files.
    /// </summary>
    public string FilePathTemplate { get; private set; }

    /// <summary>
    /// Gets the ComicInfo title template format for metadata generation.
    /// </summary>
    public string ComicInfoTitleTemplate { get; private set; }

    /// <summary>
    /// Gets the ComicInfo series template format for metadata generation.
    /// </summary>
    public string ComicInfoSeriesTemplate { get; private set; }

    /// <summary>
    /// Gets the daily execution time for the crawler cron job, or <see langword="null"/> if scheduled execution is disabled.
    /// </summary>
    public TimeSpan? DailyExecutionTime { get; private set; }

    /// <summary>
    /// Gets the Kavita integration settings, or <see langword="null"/> if not configured.
    /// </summary>
    public KavitaSettings? KavitaSettings { get; private set; }

    /// <summary>
    /// Gets the Gotify notification integration settings, or <see langword="null"/> if not configured.
    /// </summary>
    public GotifySettings? GotifySettings { get; private set; }
}
