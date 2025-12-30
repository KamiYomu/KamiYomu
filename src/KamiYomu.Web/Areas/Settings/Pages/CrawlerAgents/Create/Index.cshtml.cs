using System.ComponentModel;
using System.IO.Compression;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Pages.Shared;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Infrastructure.Storage;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Create;

public class IndexModel(DbContext dbContext, IOptions<SpecialFolderOptions> specialFolderOptions, INotificationService notificationService) : PageModel
{

    [BindProperty]
    public required InputModel Input { get; set; }


    public void OnGet(Guid? id)
    {
        CrawlerAgent? crawlerAgent = id != null ? dbContext.CrawlerAgents.FindById(id) : null;
        if (crawlerAgent == null)
        {
            Input = new InputModel();
        }
        else
        {
            Input = new InputModel()
            {
                Id = id,
                DisplayName = crawlerAgent.AssemblyName
            };
        }
    }

    public IActionResult OnPostUpload(IFormFile agentFile, CancellationToken cancellationToken)
    {
        if (agentFile == null || agentFile.Length == 0)
        {
            return BadRequest(I18n.NoFileWasFound);
        }

        string extension = Path.GetExtension(agentFile.FileName).ToLowerInvariant();
        bool isNuget = extension == ".nupkg";
        bool isDll = extension == ".dll";

        if (!isNuget && !isDll)
        {
            return BadRequest(I18n.OnlyDllOrNupkgSupported);
        }

        // Create a temp file with the correct extension
        Guid tempUploadId = Guid.NewGuid();
        string tempFileName = $"{tempUploadId}{extension}";
        string tempDirPath = Path.Combine(Path.GetTempPath(), Defaults.Worker.TempDirName);
        string tempFilePath = Path.Combine(tempDirPath, tempFileName);

        _ = Directory.CreateDirectory(tempDirPath);

        // Save to permanent storage
        _ = dbContext.CrawlerAgentFileStorage.Upload(tempUploadId, agentFile.FileName, agentFile.OpenReadStream());
        LiteDB.LiteFileInfo<Guid> fileStorage = dbContext.CrawlerAgentFileStorage.FindById(tempUploadId);
        fileStorage.SaveAs(tempFilePath, true);
        string crawlerAgentTempDir = Path.Combine(Path.GetTempPath(), Defaults.Worker.TempDirName, CrawlerAgent.GetAgentDirName(agentFile.FileName));
        string? dllPath = "";
        if (isNuget && NugetHelper.IsNugetPackage(tempFilePath))
        {
            ZipFile.ExtractToDirectory(tempFilePath, crawlerAgentTempDir, true);

            dllPath = Directory.EnumerateFiles(crawlerAgentTempDir, searchPattern: "*.dll", SearchOption.AllDirectories)
                               .FirstOrDefault();

            if (dllPath == null)
            {
                return BadRequest(I18n.NoDllFoundInNugetPackage);
            }
        }
        else
        {
            _ = Directory.CreateDirectory(crawlerAgentTempDir);
            dllPath = Path.Combine(crawlerAgentTempDir, Path.GetFileName(agentFile.FileName));
            fileStorage.SaveAs(dllPath);
        }

        // Load isolated assembly and extract metadata
        System.Reflection.Assembly assembly = CrawlerAgent.GetIsolatedAssembly(dllPath);
        Dictionary<string, string> metadata = CrawlerAgent.GetAssemblyMetadata(assembly);
        InputModel inputModel = new()
        {
            DisplayName = CrawlerAgent.GetCrawlerDisplayName(assembly),
            CrawlerInputsViewModel = new CrawlerInputsViewModel
            {
                CrawlerInputs = CrawlerAgent.GetCrawlerInputs(assembly),
                AgentMetadata = []
            },
            TempFileId = tempUploadId,
            ReadOnlyMetadata = metadata,
        };
        return Partial("_CreateForm", inputModel);
    }

    public IActionResult OnPostSave()
    {
        LiteDB.LiteFileInfo<Guid> fileStorage = dbContext.CrawlerAgentFileStorage.FindById(Input.TempFileId);

        string extension = Path.GetExtension(fileStorage.Filename).ToLowerInvariant();
        bool isNuget = extension == ".nupkg";
        bool isDll = extension == ".dll";

        if (!isNuget && !isDll)
        {
            notificationService.EnqueueErrorForNextPage(I18n.OnlyDllOrNupkgSupported);
            return Page();
        }
        string agentDirName = CrawlerAgent.GetAgentDirName(fileStorage.Filename);
        string agentDirPath = Path.Combine(specialFolderOptions.Value.AgentsDir, agentDirName);
        string tempAgentPath = Path.Combine(Path.GetTempPath(), Defaults.Worker.TempDirName, agentDirName, fileStorage.Filename);

        fileStorage.SaveAs(tempAgentPath, true);

        string? dllPath = string.Empty;
        if (isNuget && NugetHelper.IsNugetPackage(tempAgentPath))
        {
            _ = Directory.CreateDirectory(agentDirPath);

            ZipFile.ExtractToDirectory(tempAgentPath, agentDirPath, true);

            dllPath = Directory.EnumerateFiles(agentDirPath, searchPattern: "*.dll", SearchOption.AllDirectories)
                               .FirstOrDefault(p => p.EndsWith($"{CrawlerAgent.GetAgentDllFileName(fileStorage.Filename)}.dll", StringComparison.OrdinalIgnoreCase));


            if (dllPath == null)
            {
                notificationService.EnqueueErrorForNextPage(I18n.NoDllFoundInNugetPackage);
                return Page();
            }
        }
        else
        {
            dllPath = tempAgentPath;
        }

        // Register agent
        System.Reflection.Assembly assembly = CrawlerAgent.GetIsolatedAssembly(dllPath);
        IEnumerable<KamiYomu.CrawlerAgents.Core.Inputs.AbstractInputAttribute> crawlerInputs = CrawlerAgent.GetCrawlerInputs(assembly);
        string displayName = CrawlerAgent.GetCrawlerDisplayName(assembly);
        Dictionary<string, string> assemblyMetadata = CrawlerAgent.GetAssemblyMetadata(assembly);
        Dictionary<string, object> metadata = Input.CrawlerInputsViewModel.GetAgentMetadataValues();
        foreach (KamiYomu.CrawlerAgents.Core.Inputs.AbstractInputAttribute crawlerInput in crawlerInputs)
        {
            if (crawlerInput.Required)
            {
                if ((metadata.TryGetValue(crawlerInput.Name, out object? valueObj)
                   && valueObj is null)
                   || (valueObj is string valueStr && string.IsNullOrWhiteSpace(valueStr)))
                {
                    ModelState.AddModelError($"AgentMetadata[{crawlerInput.Name}]", I18n.ThisValueIsRequired);
                }
            }
        }


        if (!ModelState.IsValid)
        {
            notificationService.EnqueueErrorForNextPage(I18n.PleaseCorrectHighlightedField);
            Input = new InputModel
            {
                DisplayName = displayName,
                CrawlerInputsViewModel = new CrawlerInputsViewModel
                {
                    CrawlerInputs = crawlerInputs,
                    AgentMetadata = metadata.ToDictionary(p => p.Key, p => p.Value as string)
                },
                TempFileId = Input.TempFileId,
                ReadOnlyMetadata = assemblyMetadata,
            };
            return Page();
        }
        using CrawlerAgent crawlerAgent = new(dllPath, displayName, Input.CrawlerInputsViewModel.GetAgentMetadataValues());

        _ = dbContext.CrawlerAgents.Insert(crawlerAgent);
        _ = dbContext.CrawlerAgentFileStorage.Delete(Input.TempFileId);
        notificationService.EnqueueSuccessForNextPage(I18n.CrawlerAgentSavedSuccessfully);
        return PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Edit/Index", new
        {
            crawlerAgent.Id
        });
    }
}

public class InputModel
{
    [BindProperty]
    public Guid? Id { get; set; }
    [ReadOnly(true)]
    [BindProperty]
    public string? DisplayName { get; set; }
    [BindProperty]
    public Guid TempFileId { get; set; }

    [BindProperty]
    public CrawlerInputsViewModel CrawlerInputsViewModel { get; set; } = new();

    [BindProperty]
    public Dictionary<string, string> ReadOnlyMetadata { get; set; } = [];
}
