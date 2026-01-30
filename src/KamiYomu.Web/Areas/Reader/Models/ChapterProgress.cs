namespace KamiYomu.Web.Areas.Reader.Models;

public class ChapterProgress
{
    protected ChapterProgress()
    {
    }

    public ChapterProgress(Guid libraryId, Guid chapterId, decimal chapterNumber)
    {
        LibraryId = libraryId;
        ChapterDownloadId = chapterId;
        ChapterNumber = chapterNumber;
    }

    public void SetLastPageRead(int lastPageRead, int totalPages)
    {
        LastPageRead = lastPageRead;
        TotalPages = totalPages;
        IsCompleted = false;
        LastReadAt = DateTimeOffset.UtcNow;
    }

    public void SetAsCompleted(int totalPages)
    {
        LastPageRead = totalPages;
        TotalPages = totalPages;
        IsCompleted = true;
        LastReadAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid LibraryId { get; private set; }
    public Guid ChapterDownloadId { get; private set; }
    public decimal ChapterNumber { get; private set; }
    public int LastPageRead { get; private set; }
    public DateTimeOffset LastReadAt { get; set; }
    public bool IsCompleted { get; private set; } = false;
    public int TotalPages { get; private set; }
}
