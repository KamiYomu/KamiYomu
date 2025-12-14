---
title: Install
parent: Getting Started
nav_order: 1
---

# Installation


## Requirements

- [Docker](https://www.docker.com/get-started)

## Docker Compose

Save the following `docker-compose.yml` file to run KamiYomu with Docker:

```yml
services:
  kamiyomu:
    image: marcoscostadev/kamiyomu:latest # Check releases for latest versions
    ports:
      - "8080:8080" # HTTP Port
    environment:
        Worker__ServerAvailableNames__0:   "KamiYomu-background-1" 
        Worker__DownloadChapterQueues__0:  "download-chapter-queue-1" 
        Worker__MangaDownloadSchedulerQueues__0:  "manga-download-scheduler-queue-1" 
        Worker__DiscoveryNewChapterQueues__0:  "discovery-new-chapter-queue-1" 
        Worker__WorkerCount: 1
        Worker__MaxConcurrentCrawlerInstances: 1
        Worker__MinWaitPeriodInMilliseconds: 3000
        Worker__MaxWaitPeriodInMilliseconds: 9001
        Worker__MaxRetryAttempts: 10
        UI__DefaultLanguage: "en" 
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
      interval: 30s
      timeout: 10s
```

In the folder where you saved the `docker-compose.yml` file, run:

```bash
    docker-compose up -d
```
You will have access to the web interface at `http://localhost:8080`.
Keep in mind to map the volumes to your desired local paths. 
See the releases branchs for identifying the versions available.

## Enverionment Variables

- **Worker__ServerAvailableNames__0**

> List of Hangfire server identifiers available to process jobs.
> Each name corresponds to a distinct background worker instance;
> add more entries here if you want multiple servers to share or divide queues.
> add more entries using incrementing indexes (e.g., Worker__ServerAvailableNames__1, Worker__ServerAvailableNames__2, etc.)

- **Worker__DownloadChapterQueues__0**

> Queues dedicated to downloading individual chapters.
> add more entries using incrementing indexes (e.g., Worker__DownloadChapterQueues__1, Worker__DownloadChapterQueues__2, etc.)

- **Worker__MangaDownloadSchedulerQueues__0**

> Queues dedicated to scheduling manga downloads (manages chapter download jobs).
> add more entries using incrementing indexes (e.g., Worker__MangaDownloadSchedulerQueues__1 Worker__MangaDownloadSchedulerQueues__2, etc.)

- **Worker__DiscoveryNewChapterQueues__0**

> Queues dedicated to discovering new chapters (polling or scraping for updates).
> add more entries using incrementing indexes (e.g., Worker__DiscoveryNewChapterQueues__1, Worker__DiscoveryNewChapterQueues__2, etc.)

- **Worker__WorkerCount**

> Specifies the number of background processing threads Hangfire will spawn.
> Increasing this value allows more jobs to run concurrently, but also raises CPU load 
> and memory usage.
> Each worker consumes ~80 MB of memory on average while active 
> (actual usage may vary depending on the crawler agent implementation and system configuration).

- **Worker__MaxConcurrentCrawlerInstances**

> Defines the maximum number of crawler instances allowed to run concurrently for the same source.
> Typically set to 1 to ensure only a single crawler operates at a time, preventing duplicate work,
> resource conflicts, and potential rate‑limiting or blocking by the target system.
> This value can be increased to improve throughput if the source supports multiple concurrent requests.
>
> Note:
> - Worker__WorkerCount controls the total number of threads available.
> - Worker__MaxConcurrentCrawlerInstances limits how many threads can be used by the same crawler.
>
> Examples:
> - If Worker__MaxConcurrentCrawlerInstances = 1 and Worker__WorkerCount = 4,
>   then up to 4 different crawler agents can run independently.
> - If Worker__MaxConcurrentCrawlerInstances = 2 and Worker__WorkerCount = 6,
>   then each crawler agent can run up to 2 instances concurrently,
>   while up to 3 different crawler agents may be active at the same time.

- **Worker__MinWaitPeriodInMilliseconds**

> Minimum delay (in milliseconds) between job executions.
> Helps throttle requests to external services and avoid hitting rate limits (e.g., HTTP 423 "Too Many Requests").

- **Worker__MaxWaitPeriodInMilliseconds**

> Maximum delay (in milliseconds) between job executions.
> Provides variability in scheduling to reduce the chance of IP blocking or service throttling.

- **Worker__MaxRetryAttempts**

> Maximum number of retry attempts for failed jobs before marking them as permanently failed.

- **UI__DefaultLanguage**

> Default language for the web interface (e.g., "en", "pt-BR", "fr").