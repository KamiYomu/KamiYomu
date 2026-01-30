using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Entities;

namespace KamiYomu.Web.Areas.Reader.ViewModels;

public class ChapterViewModel
{
    public ChapterProgress ChapterProgress { get; set; }
    public Library Library { get; set; }
}
