using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel;
using System.IO.Compression;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents
{
    public class CreateModel(DbContext dbContext) : PageModel
    {

        [BindProperty]
        public CrawlerAgentCreateInputModel Input { get; set; }

       
        public void OnGet(Guid? id)
        {
            CrawlerAgent? agent = id != null ? dbContext.CrawlerAgents.FindById(id) : null;
            if (agent == null)
            {
                Input = new CrawlerAgentCreateInputModel();
            }
            else
            {
                Input = new CrawlerAgentCreateInputModel()
                {
                    Id = id,
                    DisplayName = agent.AssemblyName
                };
            }

        }

        public IActionResult OnPostUpload(IFormFile agentFile, CancellationToken cancellationToken)
        {
            if (agentFile == null || agentFile.Length == 0)
                return BadRequest("No file uploaded.");

            var extension = Path.GetExtension(agentFile.FileName).ToLowerInvariant();
            var isNuget = extension == ".nupkg";
            var isDll = extension == ".dll";

            if (!isNuget && !isDll)
                return BadRequest("Only .dll or .nupkg files are supported.");


            // Create a temp file with the correct extension
            var tempUploadId = Guid.NewGuid();
            var tempFileName = $"{tempUploadId}{extension}";
            var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);

            // Save to permanent storage
            dbContext.CrawlerAgentFileStorage.Upload(tempUploadId, agentFile.FileName, agentFile.OpenReadStream());
            var fileStorage = dbContext.CrawlerAgentFileStorage.FindById(tempUploadId);
            fileStorage.SaveAs(tempFilePath);
            var agentTempDir = Path.Combine(Path.GetTempPath(), CrawlerAgent.GetAgentDirName(agentFile.FileName));
            var dllPath = "";
            if (isNuget && NugetHelper.IsNugetPackage(tempFilePath))
            {
                ZipFile.ExtractToDirectory(tempFilePath, agentTempDir, true);

                dllPath = Directory.EnumerateFiles(agentTempDir, searchPattern: "*.dll", SearchOption.AllDirectories)
                                   .FirstOrDefault();

                if (dllPath == null)
                {
                    return BadRequest("No .dll found in NuGet package.");
                }
            }
            else
            {
                Directory.CreateDirectory(agentTempDir);
                dllPath = Path.Combine(agentTempDir, Path.GetFileName(agentFile.FileName));
                fileStorage.SaveAs(dllPath);
            }

            // Load isolated assembly and extract metadata
            var assembly = CrawlerAgent.GetIsolatedAssembly(dllPath);
            var metadata = CrawlerAgent.GetAssemblyMetadata(assembly);

            return Partial("_CrawlerAgentCreateForm", new CrawlerAgentCreateInputModel
            {
                DisplayName = CrawlerAgent.GetCrawlerDisplayName(assembly),
                CrawlerTexts = CrawlerAgent.GetCrawlerTexts(assembly),
                CrawlerPasswords = CrawlerAgent.GetCrawlerPasswords(assembly),
                CrawlerCheckBoxs = CrawlerAgent.GetCrawlerCheckBoxs(assembly),
                CrawlerSelects = CrawlerAgent.GetCrawlerSelects(assembly),
                AgentMetadata = [],
                TempFileId = tempUploadId,
                ReadOnlyMetadata = metadata,
            });
        }

        public IActionResult OnPostSave()
        {
            var fileStorage = dbContext.CrawlerAgentFileStorage.FindById(Input.TempFileId);

            var extension = Path.GetExtension(fileStorage.Filename).ToLowerInvariant();
            var isNuget = extension == ".nupkg";
            var isDll = extension == ".dll";

            if (!isNuget && !isDll)
            {
                ModelState.AddModelError("AgentFile", "Only .dll or .nupkg files are supported.");
                return Page();
            }
            var agentDir = CrawlerAgent.GetAgentDir(fileStorage.Filename);
            var agentPath = Path.Combine(CrawlerAgent.GetAgentDir(fileStorage.Filename), fileStorage.Filename);

            fileStorage.SaveAs(agentPath, true);

            string dllPath = string.Empty;
            if (isNuget && NugetHelper.IsNugetPackage(agentPath))
            {
                Directory.CreateDirectory(agentDir);

                ZipFile.ExtractToDirectory(agentPath, agentDir, true);

                dllPath = Directory.EnumerateFiles(agentDir, searchPattern: "*.dll", SearchOption.AllDirectories)
                                   .FirstOrDefault();

                if (dllPath == null)
                {
                    ModelState.AddModelError("AgentFile", "No .dll found in NuGet package.");
                    return Page();
                }
            }
            else
            {
                // Direct DLL upload
                Directory.CreateDirectory(agentDir);
                using var stream = new FileStream(agentPath, FileMode.Create);
            }

            // Register agent
            var crawlerAgent = new CrawlerAgent(dllPath, Input.DisplayName, Input.GetAgentMetadataValues());
            dbContext.CrawlerAgents.Insert(crawlerAgent);

            dbContext.CrawlerAgentFileStorage.Delete(Input.TempFileId);

            return PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Edit", new
            {
                crawlerAgent.Id
            });
        }
    }

    public class CrawlerAgentCreateInputModel
    {
        [BindProperty]
        public Guid? Id { get; set; }
        [ReadOnly(true)]
        [BindProperty]
        public string? DisplayName { get; set; }
        [BindProperty]
        public Guid TempFileId { get; set; }
        [BindProperty]
        public IEnumerable<CrawlerTextAttribute> CrawlerTexts { get; set; } = [];
        [BindProperty]
        public IEnumerable<CrawlerPasswordAttribute> CrawlerPasswords { get; set; } = [];
        [BindProperty]
        public IEnumerable<CrawlerCheckBoxAttribute> CrawlerCheckBoxs { get; set; } = [];
        [BindProperty]
        public IEnumerable<CrawlerSelectAttribute> CrawlerSelects { get; set; } = [];
        [BindProperty]
        public Dictionary<string, string> AgentMetadata { get; set; } = [];
        [BindProperty]
        public Dictionary<string, string> ReadOnlyMetadata { get; set; } = [];

        public Dictionary<string, object> GetAgentMetadataValues()
        {
            Dictionary<string, object> metadata = [];
            foreach (var item in AgentMetadata)
            {
                if(bool.TryParse(item.Value, out var boolValue)){
                    metadata[item.Key] = boolValue;
                }
                else
                {
                    metadata[item.Key] = item.Value;
                }
            }
            return metadata;
        }
    }
}
