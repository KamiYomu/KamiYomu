---
title: Create your Own Crawler Agents
parent: Crawler Agents
---

#  Create your Own Crawler Agents

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