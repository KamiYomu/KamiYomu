using System.Reflection;
using System.Text;

using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Create;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.CrawlerAgentRuntime;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Resources;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.CrawlerAgents.Create;

public class IndexModelTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.CreateCrawlerAgent", Guid.NewGuid().ToString("N"));
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ICrawlerAgentFactory> _crawlerAgentFactory = new();
    private readonly Mock<ICrawlerAgentAssemblyLoader> _crawlerAgentAssemblyLoader = new();
    private readonly IOptions<SpecialFolderOptions> _specialFolderOptions;
    private readonly IOptions<CloudflareSolverOptions> _cloudflareSolverOptions = Options.Create(new CloudflareSolverOptions());

    public IndexModelTests()
    {
        _ = Directory.CreateDirectory(_rootPath);
        _specialFolderOptions = Options.Create(new SpecialFolderOptions
        {
            AgentsDir = Path.Combine(_rootPath, "agents"),
            DbDir = Path.Combine(_rootPath, "db"),
            LogDir = Path.Combine(_rootPath, "logs"),
            MangaDir = Path.Combine(_rootPath, "manga")
        });

        // GetIsolatedAssembly/GetAssemblyMetadata/GetCrawlerInputs/GetCrawlerDisplayName all reflect
        // into the real assembly in production; here the loader is fully mocked so the uploaded
        // "dll" content never needs to be a real .NET assembly.
        _ = _crawlerAgentAssemblyLoader
            .Setup(l => l.GetIsolatedAssembly(It.IsAny<string>()))
            .Returns(new CrawlerAgentAssembly(typeof(object).Assembly, null!));
        _ = _crawlerAgentAssemblyLoader
            .Setup(l => l.GetAssemblyMetadata(It.IsAny<CrawlerAgentAssembly>()))
            .Returns(new Dictionary<string, string> { ["Version"] = "1.0.0" });
        _ = _crawlerAgentAssemblyLoader
            .Setup(l => l.GetCrawlerDisplayName(It.IsAny<Assembly>()))
            .Returns("My Crawler");
        _ = _crawlerAgentAssemblyLoader
            .Setup(l => l.GetCrawlerInputs(It.IsAny<Assembly>()))
            .Returns([new CrawlerTextAttribute("ApiKey", "Api Key", true, null, 1)]);
    }

    public void Dispose()
    {
        _dbContext.Dispose();

        try
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private IndexModel CreateModel()
    {
        return new IndexModel(
            _dbContext,
            _specialFolderOptions,
            _cloudflareSolverOptions,
            _notificationService.Object,
            _crawlerAgentFactory.Object,
            _crawlerAgentAssemblyLoader.Object)
        {
            PageContext = ServiceTestHelpers.CreatePageContext(),
            Input = new InputModel()
        };
    }

    private static IFormFile CreateFormFile(string fileName, string content = "fake-dll-bytes")
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        MemoryStream stream = new(bytes);
        return new FormFile(stream, 0, bytes.Length, "agentFile", fileName);
    }

    [Fact]
    public void OnGet_WithNoId_CreatesEmptyInputModel()
    {
        IndexModel model = CreateModel();

        model.OnGet(null);

        Assert.Null(model.Input.Id);
        Assert.Null(model.Input.DisplayName);
    }

    [Fact]
    public void OnGet_WithExistingCrawlerAgentId_PopulatesInputFromAgent()
    {
        CrawlerAgent agent = new("Existing.dll", "Existing Agent", []);
        _ = _dbContext.CrawlerAgents.Insert(agent);

        IndexModel model = CreateModel();

        model.OnGet(agent.Id);

        Assert.Equal(agent.Id, model.Input.Id);
        Assert.Equal(agent.AssemblyName, model.Input.DisplayName);
    }

    [Fact]
    public void OnPostUpload_WithUnsupportedExtension_ReturnsBadRequest()
    {
        IndexModel model = CreateModel();
        IFormFile file = CreateFormFile("agent.txt");

        IActionResult result = model.OnPostUpload(file);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(I18n.OnlyDllOrNupkgSupported, badRequest.Value);
    }

    [Fact]
    public void OnPostUpload_WithNoFile_ReturnsBadRequest()
    {
        IndexModel model = CreateModel();

        IActionResult result = model.OnPostUpload(null!);

        _ = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void OnPostUpload_WithValidDll_ReturnsPartialWithPopulatedInputModel()
    {
        IndexModel model = CreateModel();
        IFormFile file = CreateFormFile("MyCrawler.dll");

        IActionResult result = model.OnPostUpload(file);

        PartialViewResult partialViewResult = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_CreateForm", partialViewResult.ViewName);
        InputModel inputModel = Assert.IsType<InputModel>(partialViewResult.Model);
        Assert.Equal("My Crawler", inputModel.DisplayName);
        Assert.NotEqual(Guid.Empty, inputModel.TempFileId);
        Assert.Equal("1.0.0", inputModel.ReadOnlyMetadata["Version"]);
        _ = Assert.Single(inputModel.CrawlerInputsViewModel.CrawlerInputs);
    }

    [Fact]
    public void OnPostSave_WithMissingRequiredMetadata_AddsModelErrorAndReturnsPage()
    {
        IndexModel model = CreateModel();
        IFormFile file = CreateFormFile("MyCrawler.dll");
        PartialViewResult uploadResult = (PartialViewResult)model.OnPostUpload(file);
        InputModel uploadedInput = (InputModel)uploadResult.Model!;

        model.Input = uploadedInput;
        model.Input.CrawlerInputsViewModel.AgentMetadata["ApiKey"] = string.Empty;

        IActionResult result = model.OnPostSave();

        _ = Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.True(model.ModelState.ContainsKey("AgentMetadata[ApiKey]"));
        _notificationService.Verify(n => n.EnqueueErrorForNextPage(I18n.PleaseCorrectHighlightedField), Times.Once);
    }

    [Fact]
    public void OnPostSave_WithValidMetadata_InsertsAgentAndRedirects()
    {
        IndexModel model = CreateModel();
        IFormFile file = CreateFormFile("MyCrawler.dll");
        PartialViewResult uploadResult = (PartialViewResult)model.OnPostUpload(file);
        InputModel uploadedInput = (InputModel)uploadResult.Model!;

        model.Input = uploadedInput;
        model.Input.CrawlerInputsViewModel.AgentMetadata["ApiKey"] = "secret-value";

        IActionResult result = model.OnPostSave();

        RedirectToPageResult redirectResult = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/CrawlerAgents/Edit/Index", redirectResult.PageName);

        List<CrawlerAgent> savedAgents = [.. _dbContext.CrawlerAgents.FindAll()];
        _ = Assert.Single(savedAgents);
        Assert.Equal("My Crawler", savedAgents[0].DisplayName);
        _notificationService.Verify(n => n.EnqueueSuccessForNextPage(I18n.CrawlerAgentSavedSuccessfully), Times.Once);
    }
}
