using System.Globalization;

namespace KamiYomu.Web.Entities
{
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

        public Guid Id { get; private set; }
        public string Language { get; private set; }
        public int LanguageId { get; private set; }
        public bool FamilySafeMode { get; private set; } = true;
    }
}
