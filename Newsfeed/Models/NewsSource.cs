namespace Newsfeed.Models;

public sealed record NewsSource(
    string Name,
    Uri Endpoint,
    NewsSourceKind Kind);
