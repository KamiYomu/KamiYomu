using PuppeteerSharp;

Console.WriteLine("Downloading Chromium...");
var fetcher = new BrowserFetcher();
await fetcher.DownloadAsync(BrowserTag.Stable);
Console.WriteLine("Chromium download complete.");