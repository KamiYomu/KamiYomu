---
title: Install crawler agents
parent: Getting Started
nav_order: 2
---

# Install Crawler Agents

## A Crawler Agent

A crawler agent is a component responsible for searching, retrieving, and extracting metadata for manga from a specific online source. It acts as a bridge between KamiYomu and manga websites, enabling you to access a wide range of content.

## How to intall Crawler Agents in KamiYomu

1. Open the KamiYomu Web application.
1. Go to the Add-ons menu.
1. Setup the source for KamiYomu Add-ons if not already done:
1. Navigate to Add-ons > Sources
1. Click on the button "Add nuget.org as source"
1. Type "KamiYomu Crawler Agent" and click on Search or just click on Search.

<img src="/assets/tutorial/search-add-ons.gif" height="300"/>

1. Click on install button in your Crawler Agent desired.
1. Configure the add-on in the Add-ons UI as needed.

<img src="/assets/tutorial/install-add-ons.gif" height="300"/>

## List of Offical Crawler Agents

<img src="https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MangaDex/blob/main/src/KamiYomu.CrawlerAgents.MangaDex/Resources/logo.png?raw=true" width="100" height="100" />

 [MangaDex](https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MangaDex)

```sh
dotnet add package KamiYomu.CrawlerAgents.MangaDex --version <version>
```

<img src="https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MangaPark/blob/main/src/KamiYomu.CrawlerAgents.MangaPark/Resources/logo.png?raw=true" width="100" height="100" />

[Manga Park](https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MangaPark)

```sh
dotnet add package KamiYomu.CrawlerAgents.MangaPark --version <version>
```

<img src="https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MangaFire/blob/main/src/KamiYomu.CrawlerAgents.MangaFire/Resources/logo.png?raw=true" width="100" height="100" />

[Manga Fire](https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MangaFire)

```sh
dotnet add package KamiYomu.CrawlerAgents.MangaFire --version <version> 
```

<img src="https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MangaKatana/blob/main/src/KamiYomu.CrawlerAgents.MangaKatana/Resources/logo.png?raw=true" width="100" height="100" />

[Manga Katana](https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MangaKatana)

```sh
dotnet add package KamiYomu.CrawlerAgents.MangaKatana --version <version> 
```

<img src="https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MundoAvatar/blob/main/src/KamiYomu.CrawlerAgents.MundoAvatar/Resources/logo.png?raw=true" width="100" height="100" />

[Mundo Avatar](https://github.com/KamiYomu/KamiYomu.CrawlerAgents.MundoAvatar)

```sh
 dotnet add package KamiYomu.CrawlerAgents.MundoAvatar --version <version> 
```  