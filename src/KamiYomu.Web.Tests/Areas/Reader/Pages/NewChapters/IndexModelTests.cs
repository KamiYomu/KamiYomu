using KamiYomu.Web.Areas.Reader.Pages.NewChapters;

namespace KamiYomu.Web.Tests.Areas.Reader.Pages.NewChapters;

public class IndexModelTests
{
    [Fact]
    public void OnGet_DoesNotThrow()
    {
        IndexModel model = new();

        Exception? exception = Record.Exception(model.OnGet);

        Assert.Null(exception);
    }
}
