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

        internal void SetFamilyMode(bool familyMode)
        {
            FamilyMode = familyMode;
        }

        public Guid Id { get; private set; }
        public string Language { get; private set; }
        public int LanguageId { get; private set; }
        public bool FamilyMode { get; private set; } = true;
    }
}
