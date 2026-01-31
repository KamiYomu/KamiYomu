using KamiYomu.Web.ViewComponents;

namespace KamiYomu.Web.Tests.ViewComponents;

public class TablePaginationTests
{
    [Theory]
    [InlineData(10, 5, 2)]  // 10 items, 5 per page = 2 pages
    [InlineData(11, 5, 3)]  // 11 items, 5 per page = 3 pages (Ceiling check)
    [InlineData(0, 5, 0)]   // 0 items = 0 pages
    public void TotalPages_ShouldCalculateCorrectly(int totalItems, int pageSize, int expected)
    {
        // Arrange
        TablePaginationViewModel vm = new()
        {
            CurrentPage = 1,
            TotalItems = totalItems,
            PageSize = pageSize,
            PageUrlTemplate = ""
        };

        // Assert
        Assert.Equal(expected, vm.TotalPages);
    }

    [Theory]
    // EdgeCount = 2, Window = 1, CurrentPage = 5, TotalPages = 10
    [InlineData(1, true)]  // Within EdgeCount (start)
    [InlineData(2, true)]  // Within EdgeCount (start)
    [InlineData(3, false)] // Outside Edge, Window, and End
    [InlineData(4, true)]  // Inside Window (Current - 1)
    [InlineData(5, true)]  // Current Page
    [InlineData(6, true)]  // Inside Window (Current + 1)
    [InlineData(9, true)]  // Within EdgeCount (end)
    [InlineData(10, true)] // Within EdgeCount (end)
    public void ShouldShow_ShouldReturnCorrectVisibility(int pageToTest, bool expectedVisibility)
    {
        // Arrange
        TablePaginationViewModel vm = new()
        {
            CurrentPage = 5,
            TotalItems = 100,
            PageSize = 10, // TotalPages = 10
            EdgeCount = 2,
            Window = 1,
            PageUrlTemplate = ""
        };

        // Act
        bool result = vm.ShouldShow(pageToTest);

        // Assert
        Assert.Equal(expectedVisibility, result);
    }

    [Theory]
    [InlineData("?CurrentPage=0&libId=5", 2, "?CurrentPage=2&libId=5")]
    [InlineData("?libId=5&CurrentPage=0", 99, "?libId=5&CurrentPage=99")]
    [InlineData("?CurrentPage=oldValue&sort=asc", 1, "?CurrentPage=1&sort=asc")]
    public void GetPageUrl_ShouldReplacePlaceholderWithRegex(string template, int targetPage, string expectedUrl)
    {
        // Arrange
        TablePaginationViewModel vm = new()
        {
            CurrentPage = 1,
            TotalItems = 10,
            PageSize = 10,
            PageUrlTemplate = template
        };

        // Act
        string result = vm.GetPageUrl(targetPage);

        // Assert
        Assert.Equal(expectedUrl, result);
    }
}
