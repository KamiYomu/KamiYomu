# KamiYomu — Your Self-Hosted Manga Crawler

![KamiYomu Owl Logo](./Inkscape/logo-watermark.svg)

**KamiYomu** is a powerful, extensible manga crawler built for manga enthusiasts who want full control over their collection. It scans and downloads manga from supported websites, stores them locally, and lets you host your own private manga reader—no ads, no subscriptions, no limits.

---

## ✨ Features

- 🔍 **Automated Crawling**  
  Fetch chapters from supported manga sites with ease.

- 💾 **Local Storage**  
  Keep your manga files on your own server or device.

- 🧩 **Plugin Architecture**  
  Add support for new sources or customize crawling logic.

- 🛠️ **Built with .NET Razor Pages**  
  Lightweight, maintainable, and easy to extend.

---

## 🚀 Why KamiYomu?

Whether you're cataloging rare series, powering a personal manga dashboard, or seeking a cleaner alternative to bloated online readers, KamiYomu puts you in control of how you access and organize manga content. It’s a lightweight, developer-friendly crawler built for clarity, extensibility, and respectful use of publicly accessible sources. Content availability and usage rights depend on the licensing terms of each source — KamiYomu simply provides the tools.
 
 <br/>
 <img src="./screenshots/welcome-page.jpeg" alt="Welcome Page" width="600"/>
 <br/>

## Requirements

- [Docker](https://www.docker.com/get-started)


## 📦 Getting Started

save the following `docker-compose.yml` file to run KamiYomu with Docker:

```yml
services:
  kamiyomu:
    image: marcoscostadev/kamiyomu:latest # Check releases for latest versions
    ports:
      - "8080:8080" # HTTP Port
    environment:
        # List of Hangfire server identifiers available to process jobs.
        # Each name corresponds to a distinct background worker instance;
        # add more entries here if you want multiple servers to share or divide queues.
        # add more entries using incrementing indexes (e.g., Worker__ServerAvailableNames__1, Worker__ServerAvailableNames__2, etc.)
        Worker__ServerAvailableNames__0:   "KamiYomu-background-1" 
        

        # Queues dedicated to downloading individual chapters.
        # add more entries using incrementing indexes (e.g., Worker__DownloadChapterQueues__1, Worker__DownloadChapterQueues__2, etc.)
        Worker__DownloadChapterQueues__0:  "download-chapter-queue-1" 

        # Queues dedicated to scheduling manga downloads (manages chapter download jobs).
        # add more entries using incrementing indexes (e.g., Worker__MangaDownloadSchedulerQueues__1, Worker__MangaDownloadSchedulerQueues__2, etc.)
        Worker__MangaDownloadSchedulerQueues__0:  "manga-download-scheduler-queue-1" 

        # Queues dedicated to discovering new chapters (polling or scraping for updates).
        # add more entries using incrementing indexes (e.g., Worker__DiscoveryNewChapterQueues__1, Worker__DiscoveryNewChapterQueues__2, etc.)
        Worker__DiscoveryNewChapterQueues__0:  "discovery-new-chapter-queue-1" 

        # Specifies the number of background processing threads Hangfire will spawn.
        # Increasing this value allows more jobs to run concurrently, but also raises CPU load 
        # and memory usage.
        # Each worker consumes ~80 MB of memory on average while active 
        # (actual usage may vary depending on the crawler agent implementation and system configuration).
        Worker__WorkerCount: 1

        # Defines the maximum number of crawler instances allowed to run concurrently for the same source.
        # Typically set to 1 to ensure only a single crawler operates at a time, preventing duplicate work,
        # resource conflicts, and potential rate‑limiting or blocking by the target system.
        # This value can be increased to improve throughput if the source supports multiple concurrent requests.
        #
        # Note:
        # - Worker__WorkerCount controls the total number of threads available.
        # - Worker__MaxConcurrentCrawlerInstances limits how many threads can be used by the same crawler.
        #
        # Examples:
        # - If Worker__MaxConcurrentCrawlerInstances = 1 and Worker__WorkerCount = 4,
        #   then up to 4 different crawler agents can run independently.
        # - If Worker__MaxConcurrentCrawlerInstances = 2 and Worker__WorkerCount = 6,
        #   then each crawler agent can run up to 2 instances concurrently,
        #   while up to 3 different crawler agents may be active at the same time.
        Worker__MaxConcurrentCrawlerInstances: 1

        # Minimum delay (in milliseconds) between job executions.
        # Helps throttle requests to external services and avoid hitting rate limits (e.g., HTTP 423 "Too Many Requests").
        Worker__MinWaitPeriodInMilliseconds: 3000

        # Maximum delay (in milliseconds) between job executions.
        # Provides variability in scheduling to reduce the chance of IP blocking or service throttling.
        Worker__MaxWaitPeriodInMilliseconds: 9001

        # Maximum number of retry attempts for failed jobs before marking them as permanently failed.
        Worker__MaxRetryAttempts: 10

        # Default language for the web interface (e.g., "en", "pt-BR", "fr").
        UI__DefaultLanguage: "en" 
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
      interval: 30s
      timeout: 10s
      retries: 3
    volumes:
      - ./AppData/manga:/manga # Your desired local path for manga storage
      - Kamiyomu_database:/db
      - kamiyomu_agents:/agents
      - kamiyomu_logs:/logs

volumes:
  kamiyomu_agents:
  Kamiyomu_database:
  kamiyomu_logs:
```

In the folder where you saved the `docker-compose.yml` file, run:

```bash
    docker-compose up -d
```
You will have access to the web interface at `http://localhost:8080`.
Keep in mind to map the volumes to your desired local paths. 
See the releases branchs for identifying the versions available.

Configure your sources and crawler agents 

Download crawler agents from NuGet Package from [here](https://github.com/orgs/KamiYomu/packages) and upload them in [Crawler Agents](http://localhost:8080/Settings/CrawlerAgents).

## 🛠️ Development Setup

We recommend using Visual Studio 2022 or later with the .NET 8 SDK installed. 
However, you can also run KamiYomu using VsCode.

1. Fork the repository
2. Select the develop branch (`git checkout develop`)
3. Create your feature branch (`git checkout -b feature/AmazingFeature`)
4. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
5. Push to the branch (`git push origin feature/AmazingFeature`)
6. Open a Pull Request against the ``develop`` branch

### Using Visual Studio

- Docker: [Download here](https://www.docker.com/get-started)
- Visual Studio: [Download here](https://visualstudio.microsoft.com/downloads/)
- .NET 8 SDK: [Download here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

1. **Clone the repository**
   ```bash
   git clone https://github.com/KamiYomu/KamiYomu.Web.git
	```
2. Open the solution in Visual Studio in `/src/KamiYomu.Web.sln`
3. Set `docker-compose` project as **startup project** (Right-click on project, select `Set As Startup Project.`).
4. Run it 

### Using VsCode

To get started with local development using Visual Studio Code, ensure the following tools are installed:

**Required Tools**

- Docker: [Download here](https://www.docker.com/get-started)
- Visual Studio Code [Download Here](https://code.visualstudio.com/)
- C# Dev Kit Extension [Install](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
- Docker Extension for VS Code [Install](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-docker)

Note: Make sure Docker is installed and running on your machine.

1. Clone the Repository

```bash
    git clone https://github.com/KamiYomu/KamiYomu.Web.git
```
2. Running the Project in VS Code
- Open the `./src/` folder in VS Code.
- Navigate to the "Run and Debug" tab (Ctrl+Shift+D) or press `F5`.
- Select the launch configuration: "Attach to .NET Core in Docker".
- Click the ▶️ Start Debugging button.


This project includes predefined tasks to build and run the Docker container automatically.
If you install all required extensions, the project will run and open the browser in http://localhost:8080
> NOTE:  You may see a window with some error related `=> ERROR [kamiyomu.web internal] load metadata for mcr.microsoft.com/dotnet/sdk:8.0`, just click on `abort` button then try again.

## 🧩 Create your First Crawler Agent


To create your first crawler agent, follow these steps:
1. **Set Up a New Project**: Create a new Class Library project in Visual Studio or your preferred IDE.
1. **Add References**: Add references to the necessary KamiYomu packages from NuGet `KamiYomu.CrawlerAgents.Core`.
1. **Implement the 5 methods from ICrawlerAgent Interface**: Create a class that implements the `ICrawlerAgent` interface. This class will contain the logic for crawling a specific manga source.

```csharp

/// <summary>
/// Defines a contract for manga crawling agents that support search, retrieval, and metadata extraction.
/// </summary>
public interface ICrawlerAgent : IDisposable
{
    /// <summary>
    /// Asynchronously retrieves the favicon URI associated with the crawler's target site.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A <see cref="Task{Uri}"/> representing the favicon location.</returns>
    Task<Uri> GetFaviconAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Searches for manga titles matching the specified name, using either traditional pagination or a continuation token.
    /// </summary>
    /// <param name="titleName">The title or keyword to search for.</param>
    /// <param name="paginationOptions">Pagination parameters, supporting both page-based and continuation token-based pagination.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A paged result containing a collection of matching <see cref="Manga"/> entries.</returns>
    Task<PagedResult<Manga>> SearchAsync(string titleName, PaginationOptions paginationOptions, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves detailed information about a specific manga by its unique identifier.
    /// </summary>
    /// <param name="id">The unique ID of the manga.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A <see cref="Task{Manga}"/> containing the manga details.</returns>
    Task<Manga> GetByIdAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a paged list of chapters for the specified manga.
    /// </summary>
    /// <param name="manga">The manga object.</param>
    /// <param name="paginationOptions">Pagination parameters, supporting both page-based and continuation token-based pagination.</param>
    /// <returns>A paged result containing a collection of <see cref="Chapter"/> entries.</returns>
    Task<PagedResult<Chapter>> GetChaptersAsync(Manga manga, PaginationOptions paginationOptions, CancellationToken cancellationToken);
    /// <summary>
    /// Retrieves the list of page images associated with a given manga chapter.
    /// </summary>
    /// <param name="chapter">The chapter entity containing metadata and identifiers.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A collection of <see cref="Page"/> objects representing individual chapter pages.</returns>
    Task<IEnumerable<Page>> GetChapterPagesAsync(Chapter chapter, CancellationToken cancellationToken);
}
```
1. **Build the Project**: Compile your project to generate the DLL file.
1. **Deploy the Crawler Agent**: Upload the compiled DLL to the KamiYomu web interface under the "Crawler Agents" section. Or publish the package in NuGet.Org
1. **Configure and Use**: Once uploaded, configure the crawler agent in KamiYomu and start crawling manga from the supported source.

Do you want a reference implementation? Check:
- [KamiYomu.CrawlerAgents.MangaDex](https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MangaDex) if you crawler will consume a web api.
- [KamiYomu.CrawlerAgents.MangaKatana](https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MangaKatana)  if you crawler will consume a web page.
- [KamiYomu.CrawlerAgents.MangaFire](https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MangaFire)  another example to consume a web page.
> NOTE: Make sure to use a Validator console app to ensure your crawler agent meets all requirements before deploying it to KamiYomu.


Consider using this `<PropertyGroup>` in your csproj, adjust the title accorgly

```xml
	<PropertyGroup>
		<Title>My Crawler Agent</Title>
		<Description>A dedicated crawler agent for accessing public data from My Personal Stuff. Built on KamiYomu.CrawlerAgents.Core, it enables efficient search, metadata extraction, and integration with the KamiYomu platform.</Description>
		<Authors>MyName</Authors>
		<Owners>MyName</Owners>
		<PackageProjectUrl>https://github.com/MyProjectUrl</PackageProjectUrl>
		<RepositoryUrl>https://github.com/MyRepositoryUrl</RepositoryUrl>
		<RepositoryType>git</RepositoryType>
		<PackageTags>kamiyomu-crawler-agents;manga-download</PackageTags>
		<PackageLicenseExpression>GPL-3.0-only</PackageLicenseExpression>
		<Copyright>© Personal. Licensed under GPL-3.0.</Copyright>
		<PackageIconUrl>https://raw.githubusercontent.com/MyPackageLogoUrl</PackageIconUrl>
		<PackageIcon>Resources/logo.png</PackageIcon>
		<PackageReadmeFile>README.md</PackageReadmeFile>
	</PropertyGroup>
```

The Package Tag `<PackageTags>kamiyomu-crawler-agents</PackageTags>` is required to be showed in KamiYomu add-ons
See the existing projects to use as a reference for your csproj.

## Debugging Your NuGet Package in KamiYomu

This guide explains how to build, import, and debug a NuGet package for use in KamiYomu.

---

### 1. Configure Your Project

Add the following snippet to your `.csproj` file.  
This ensures that a NuGet package is generated during the **Debug** build:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <GeneratePackageOnBuild>True</GeneratePackageOnBuild>
  <IncludeSymbols>True</IncludeSymbols>
  <IncludeSource>True</IncludeSource>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>
```

### 2. Build the Project

Run a build in **Debug** mode.  
The generated NuGet package (.nupkg) and symbol file (.pdb) will be located in:

### 3. Import the Package into KamiYomu

1. Open KamiYomu.
2. Navigate to **Crawler Agents** in the menu.
3. Import your NuGet package (.nupkg) from the bin/Debug folder.

### 4. Copy the Symbol File

Copy the .pdb file from your bin/Debug folder into:

`src\AppData\agents\{your-crawler}\lib\net8.0`

This allows Visual Studio to map your source code during debugging.

### 5. Debugging in KamiYomu

KamiYomu uses a decorator class to invoke agent methods:

`src\KamiYomu.Web\Entities\CrawlerAgentDecorator.cs`

- Set a breakpoint in any call within this class.
- When Visual Studio hits the breakpoint, step into the call.
- Your agent’s source code will be displayed and debuggable.

## 🧠 Tech Stack- .NET 8 Razor Pages
- Hangfire for job scheduling
- LiteDB for lightweight persistence
- HTMX + Bootstrap for dynamic UI
- Plugin-based architecture for source extensibility

## 📜 License
This project is licensed under AGPL-3.0. See the LICENSE file for details.

## 🤝 Contributing
Pull requests are welcome! If you have ideas for new features, plugin sources, or UI improvements, feel free to open an issue or submit a PR.

## 💬 Contact
Questions, feedback, or bug reports? Reach out via GitHub Issues or start a discussion.