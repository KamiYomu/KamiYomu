using System.Globalization;
using System.Linq.Expressions;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.ViewComponents;

using LiteDB;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.ViewComponents;

public class FamilySafeViewComponentTests
{
    private readonly Mock<ILiteCollection<UserPreference>> _mockCollection;
    private readonly Mock<IOptions<StartupOptions>> _mockOptions;
    private readonly DbContext _realContext;

    public FamilySafeViewComponentTests()
    {
        _mockCollection = new Mock<ILiteCollection<UserPreference>>();
        _mockOptions = new Mock<IOptions<StartupOptions>>();

        _realContext = new DbContext(":memory:");
    }

    [Theory]
    [InlineData(true, "btn btn-outline-success no-hover", "bi-shield-check text-success")]
    [InlineData(false, "btn btn-outline-secondary no-hover", "bi-shield-exclamation text-danger")]
    public void Invoke_WhenUserPreferenceExists_UsesValueFromDatabase(bool dbValue, string expectedBtn, string expectedIcon)
    {
        // Arrange
        CreateUserPreference(dbValue);

        _ = _mockOptions.Setup(o => o.Value).Returns(new StartupOptions { FamilyMode = !dbValue });

        FamilySafeViewComponent component = new(_realContext, _mockOptions.Object);

        // Act
        IViewComponentResult result = component.Invoke();

        // Assert
        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        FamilySafeViewComponentModel model = Assert.IsType<FamilySafeViewComponentModel>(viewResult.ViewData.Model);

        Assert.Equal(dbValue, model.IsFamilySafe);
        Assert.Equal(expectedBtn, model.ButtonClass);
        Assert.Equal(expectedIcon, model.IconClass);
    }

    [Fact]
    public void Invoke_WhenUserPreferenceIsNull_FallsBackToStartupOptions()
    {
        // Arrange
        _ = _mockCollection
            .Setup(x => x.FindOne(It.IsAny<Expression<Func<UserPreference, bool>>>()))
            .Returns((UserPreference)null!);

        _ = _mockOptions.Setup(o => o.Value).Returns(new StartupOptions { FamilyMode = true });

        FamilySafeViewComponent component = new(_realContext, _mockOptions.Object);

        // Act
        IViewComponentResult result = component.Invoke();

        // Assert
        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        FamilySafeViewComponentModel model = Assert.IsType<FamilySafeViewComponentModel>(viewResult.ViewData.Model);

        Assert.True(model.IsFamilySafe);
    }

    private void CreateUserPreference(bool familySafe)
    {
        UserPreference pref = new(CultureInfo.CurrentCulture);
        pref.SetFamilySafeMode(familySafe);
        _ = _realContext.UserPreferences.Insert(pref);

        var result = _realContext.UserPreferences.FindAll().ToList();
    }
}
