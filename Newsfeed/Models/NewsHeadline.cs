namespace Newsfeed.Models;

public sealed class NewsHeadline
{
    public NewsHeadline(
        string sourceName,
        string title,
        string url,
        DateTimeOffset publishedAt,
        bool isMock = false,
        string? summary = null,
        bool isLiveUpdate = false)
    {
        SourceName = sourceName;
        Title = title;
        Url = url;
        PublishedAt = publishedAt;
        IsMock = isMock;
        Summary = summary;
        IsLiveUpdate = isLiveUpdate;
    }

    public string SourceName { get; set; }

    public string Title { get; set; }

    public string Url { get; set; }

    public DateTimeOffset PublishedAt { get; set; }

    public bool IsMock { get; set; }

    public string? Summary { get; set; }

    public bool IsLiveUpdate { get; set; }

    public string RelativePublishedText => FormatRelativePublishedText();

    private string FormatRelativePublishedText()
    {
        var age = DateTimeOffset.Now - PublishedAt;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{Math.Max(1, (int)Math.Floor(age.TotalMinutes))}m ago";
        }

        if (age < TimeSpan.FromDays(2))
        {
            return $"{Math.Max(1, (int)Math.Floor(age.TotalHours))}h ago";
        }

        if (age < TimeSpan.FromDays(7))
        {
            return $"{Math.Max(1, (int)Math.Floor(age.TotalDays))}d ago";
        }

        return PublishedAt.ToLocalTime().ToString("MMM d");
    }
}
