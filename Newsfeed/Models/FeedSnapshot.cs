namespace Newsfeed.Models;

public sealed record FeedSnapshot(
    IReadOnlyList<NewsHeadline> Headlines,
    bool UsedFallbackData,
    DateTimeOffset RefreshedAt,
    IReadOnlyList<string> Errors);
