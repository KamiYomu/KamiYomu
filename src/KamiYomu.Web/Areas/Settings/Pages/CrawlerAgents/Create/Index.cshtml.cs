using KamiYomu.Web.Areas.Settings.Pages.Shared;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel;
using System.IO.Compression;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Create;

public class IndexModel(DbContext dbContext, INotificationService notificationService) : PageModel
{

    [BindProperty]
    public InputModel Input { get; set; }


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

        var extension = Path.GetExtension(agentFile.FileName).ToLowerInvariant();
        var isNuget = extension == ".nupkg";
        var isDll = extension == ".dll";

        if (!isNuget && !isDll)
        {
            return BadRequest(I18n.OnlyDllOrNupkgSupported);
        }

        // Create a temp file with the correct extension
        var tempUploadId = Guid.NewGuid();
        var tempFileName = $"{tempUploadId}{extension}";
        var tempDirPath = Path.Combine(Path.GetTempPath(), AppOptions.Defaults.Worker.TempDirName); 
        var tempFilePath = Path.Combine(tempDirPath, tempFileName);

        Directory.CreateDirectory(tempDirPath);

        // Save to permanent storage
        dbContext.CrawlerAgentFileStorage.Upload(tempUploadId, agentFile.FileName, agentFile.OpenReadStream());
        var fileStorage = dbContext.CrawlerAgentFileStorage.FindById(tempUploadId);
        fileStorage.SaveAs(tempFilePath, true);
        var crawlerAgentTempDir = Path.Combine(Path.GetTempPath(), AppOptions.Defaults.Worker.TempDirName, CrawlerAgent.GetAgentDirName(agentFile.FileName));
        var dllPath = "";
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
            Directory.CreateDirectory(crawlerAgentTempDir);
            dllPath = Path.Combine(crawlerAgentTempDir, Path.GetFileName(agentFile.FileName));
            fileStorage.SaveAs(dllPath);
        }

        // Load isolated assembly and extract metadata
        var assembly = CrawlerAgent.GetIsolatedAssembly(dllPath);
        var metadata = CrawlerAgent.GetAssemblyMetadata(assembly);
        var inputModel = new InputModel
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

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var fileStorage = dbContext.CrawlerAgentFileStorage.FindById(Input.TempFileId);

        var extension = Path.GetExtension(fileStorage.Filename).ToLowerInvariant();
        var isNuget = extension == ".nupkg";
        var isDll = extension == ".dll";

        if (!isNuget && !isDll)
        {
            notificationService.EnqueueErrorForNextPage(I18n.OnlyDllOrNupkgSupported);
            return Page();
        }
        var agentDir = CrawlerAgent.GetAgentDir(fileStorage.Filename);
        var agentPath = Path.Combine(CrawlerAgent.GetAgentDir(fileStorage.Filename), fileStorage.Filename);

        fileStorage.SaveAs(agentPath, true);

        string? dllPath = string.Empty;
        if (isNuget && NugetHelper.IsNugetPackage(agentPath))
        {
            Directory.CreateDirectory(agentDir);

            ZipFile.ExtractToDirectory(agentPath, agentDir, true);

            dllPath = Directory.EnumerateFiles(agentDir, searchPattern: "*.dll", SearchOption.AllDirectories)
                               .FirstOrDefault(p => p.EndsWith($"{CrawlerAgent.GetAgentDirName(fileStorage.Filename)}.dll", StringComparison.OrdinalIgnoreCase));


            if (dllPath == null)
            {
                notificationService.EnqueueErrorForNextPage(I18n.NoDllFoundInNugetPackage);
                return Page();
            }
        }
        else
        {
            dllPath = agentPath;
        }

        // Register agent
        var assembly = CrawlerAgent.GetIsolatedAssembly(dllPath);
        var crawlerInputs = CrawlerAgent.GetCrawlerInputs(assembly);
        var displayName = CrawlerAgent.GetCrawlerDisplayName(assembly);
        var assemblyMetadata = CrawlerAgent.GetAssemblyMetadata(assembly);
        var metadata = Input.CrawlerInputsViewModel.GetAgentMetadataValues();
        foreach (var crawlerInput in crawlerInputs)
        {
            if (crawlerInput.Required)
            {
                if (metadata.TryGetValue(crawlerInput.Name, out var valueObj)
                   && valueObj is null
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
        using var crawlerAgent = new CrawlerAgent(dllPath, displayName, Input.CrawlerInputsViewModel.GetAgentMetadataValues());

        dbContext.CrawlerAgents.Insert(crawlerAgent);
        dbContext.CrawlerAgentFileStorage.Delete(Input.TempFileId);
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
