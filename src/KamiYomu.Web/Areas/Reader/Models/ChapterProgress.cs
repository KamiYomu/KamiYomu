namespace KamiYomu.Web.Areas.Reader.Models;

public class ChapterProgress
{
    protected ChapterProgress()
    {
    }

    public ChapterProgress(Guid libraryId, Guid chapterId)
    {
        LibraryId = libraryId;
        ChapterId = chapterId;
    }

    public void SetLastPageRead(int lastPageRead)
    {
        LastPageRead = lastPageRead;
        IsCompleted = false;
        LastReadAt = DateTimeOffset.UtcNow;
    }

    public void SetAsCompleted()
    {
        LastPageRead = 0;
        IsCompleted = true;
        LastReadAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid LibraryId { get; private set; }
    public Guid ChapterId { get; private set; }
    public int LastPageRead { get; private set; }
    public DateTimeOffset LastReadAt { get; set; }
    public bool IsCompleted { get; private set; } = false;
}
