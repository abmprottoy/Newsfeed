using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newsfeed.Models;

namespace Newsfeed.Services;

public sealed class FeedService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Regex _anchorRegex = new("<a\\s[^>]*href=\"(?<href>[^\"]+)\"[^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private readonly Regex _scriptJsonRegex = new("<script[^>]*type=\"application/ld\\+json\"[^>]*>(?<json>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private readonly Regex _alJazeeraLiveBlogRegex = new("(?:https://www\\.aljazeera\\.com)?(?<path>/news/liveblog/[^\"'\\s<>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public FeedService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NewsfeedTicker/1.0 (+https://github.com/microsoft/WindowsAppSDK)");
    }

    public IReadOnlyList<NewsSource> DefaultSources { get; } =
    [
        new("Al Jazeera Live", new Uri("https://www.aljazeera.com/"), NewsSourceKind.AlJazeeraLiveBlog),
        new("BBC World", new Uri("http://feeds.bbci.co.uk/news/world/rss.xml"), NewsSourceKind.Rss),
        new("The Guardian World", new Uri("https://www.theguardian.com/world/rss"), NewsSourceKind.Rss),
        new("Bloomberg Politics", new Uri("https://feeds.bloomberg.com/politics/news.rss"), NewsSourceKind.Rss),
        new("Bloomberg Markets", new Uri("https://feeds.bloomberg.com/markets/news.rss"), NewsSourceKind.Rss),
        new("WSJ World", new Uri("https://feeds.a.dj.com/rss/RSSWorldNews.xml"), NewsSourceKind.Rss)
    ];

    public async Task<FeedSnapshot> RefreshAsync(IEnumerable<string> focusTerms, CancellationToken cancellationToken)
    {
        var sourceTasks = DefaultSources.Select(source => ReadSourceAsync(source, cancellationToken)).ToArray();
        var results = await Task.WhenAll(sourceTasks);

        var errors = results.Where(result => result.Error is not null).Select(result => result.Error!).ToList();
        var combined = results.SelectMany(result => result.Headlines).ToList();
        var filtered = FilterAndNormalize(combined, focusTerms);

        if (filtered.Count == 0)
        {
            return new FeedSnapshot(BuildFallbackHeadlines(), true, DateTimeOffset.Now, errors);
        }

        return new FeedSnapshot(filtered, false, DateTimeOffset.Now, errors);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<SourceReadResult> ReadSourceAsync(NewsSource source, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(source.Endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            var headlines = source.Kind switch
            {
                NewsSourceKind.AlJazeeraLiveBlog => await ReadAlJazeeraLiveBlogAsync(source, content, cancellationToken),
                NewsSourceKind.Rss => ReadRss(source, content),
                NewsSourceKind.Html => ReadHtml(source, content),
                _ => []
            };

            return new SourceReadResult(headlines, null);
        }
        catch (Exception ex)
        {
            return new SourceReadResult([], $"{source.Name}: {ex.Message}");
        }
    }

    private IReadOnlyList<NewsHeadline> ReadRss(NewsSource source, string xmlContent)
    {
        var document = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
        var items = document.Descendants("item")
            .Select(item =>
            {
                var title = item.Element("title")?.Value.Trim();
                var link = item.Element("link")?.Value.Trim();
                var dateText = item.Element("pubDate")?.Value.Trim();
                var summary = item.Element("description")?.Value.Trim();
                var publishedAt = DateTimeOffset.TryParse(dateText, out var parsedDate)
                    ? parsedDate
                    : DateTimeOffset.Now;

                return CreateHeadline(source.Name, title, link, publishedAt, summary: summary);
            })
            .Where(headline => headline is not null)
            .Cast<NewsHeadline>()
            .ToList();

        if (items.Count > 0)
        {
            return items;
        }

        return document.Descendants()
            .Where(node => node.Name.LocalName == "entry")
            .Select(entry =>
            {
                var title = entry.Elements().FirstOrDefault(element => element.Name.LocalName == "title")?.Value.Trim();
                var link = entry.Elements().FirstOrDefault(element => element.Name.LocalName == "link")?.Attribute("href")?.Value.Trim();
                var dateText = entry.Elements().FirstOrDefault(element => element.Name.LocalName is "updated" or "published")?.Value.Trim();
                var summary = entry.Elements().FirstOrDefault(element => element.Name.LocalName is "summary" or "content")?.Value.Trim();
                var publishedAt = DateTimeOffset.TryParse(dateText, out var parsedDate)
                    ? parsedDate
                    : DateTimeOffset.Now;

                return CreateHeadline(source.Name, title, link, publishedAt, summary: summary);
            })
            .Where(headline => headline is not null)
            .Cast<NewsHeadline>()
            .ToList();
    }

    private IReadOnlyList<NewsHeadline> ReadHtml(NewsSource source, string htmlContent)
    {
        var headlines = new List<NewsHeadline>();

        foreach (Match match in _anchorRegex.Matches(htmlContent))
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value.Trim());
            var rawText = match.Groups["text"].Value;
            var title = CollapseWhitespace(RemoveTags(WebUtility.HtmlDecode(rawText)));

            if (string.IsNullOrWhiteSpace(title) || title.Length < 28 || title.Length > 180)
            {
                continue;
            }

            if (!href.Contains("/news/", StringComparison.OrdinalIgnoreCase) &&
                !href.Contains("/liveblog/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var absoluteUrl = Uri.TryCreate(source.Endpoint, href, out var absoluteUri)
                ? absoluteUri.ToString()
                : href;

            var headline = CreateHeadline(source.Name, title, absoluteUrl, DateTimeOffset.Now);
            if (headline is not null)
            {
                headlines.Add(headline);
            }
        }

        return headlines
            .DistinctBy(headline => $"{headline.SourceName}:{headline.Title}")
            .Take(20)
            .ToList();
    }

    private async Task<IReadOnlyList<NewsHeadline>> ReadAlJazeeraLiveBlogAsync(NewsSource source, string homepageContent, CancellationToken cancellationToken)
    {
        var liveBlogUrl = FindCurrentAlJazeeraLiveBlogUrl(source.Endpoint, homepageContent);
        if (liveBlogUrl is null)
        {
            return [];
        }

        using var response = await _httpClient.GetAsync(BuildAlJazeeraAmpUrl(liveBlogUrl), cancellationToken);
        response.EnsureSuccessStatusCode();
        var ampContent = await response.Content.ReadAsStringAsync(cancellationToken);

        return ReadAlJazeeraAmpLiveBlog(source.Name, liveBlogUrl, ampContent);
    }

    private IReadOnlyList<NewsHeadline> ReadAlJazeeraAmpLiveBlog(string sourceName, string liveBlogUrl, string ampContent)
    {
        var headlines = new List<NewsHeadline>();

        foreach (Match match in _scriptJsonRegex.Matches(ampContent))
        {
            var json = WebUtility.HtmlDecode(match.Groups["json"].Value.Trim());
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                AppendLiveBlogUpdates(document.RootElement, sourceName, liveBlogUrl, headlines);
            }
            catch (JsonException)
            {
            }
        }

        return headlines
            .DistinctBy(headline => $"{headline.SourceName}:{headline.Title}:{headline.Url}")
            .OrderByDescending(headline => headline.PublishedAt)
            .ToList();
    }

    private static void AppendLiveBlogUpdates(JsonElement element, string sourceName, string liveBlogUrl, ICollection<NewsHeadline> headlines)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AppendLiveBlogUpdates(item, sourceName, liveBlogUrl, headlines);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!string.Equals(GetJsonString(element, "@type"), "LiveBlogPosting", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!element.TryGetProperty("liveBlogUpdate", out var updates) || updates.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var update in updates.EnumerateArray())
        {
            var title = GetJsonString(update, "headline");
            var url = CleanTrackedUrl(GetJsonString(update, "url")) ?? liveBlogUrl;
            var body = CollapseWhitespace(RemoveTags(WebUtility.HtmlDecode(GetJsonString(update, "articleBody") ?? string.Empty)));
            var publishedAtText = GetJsonString(update, "dateModified") ?? GetJsonString(update, "datePublished");
            var publishedAt = DateTimeOffset.TryParse(publishedAtText, out var parsedDate)
                ? parsedDate
                : DateTimeOffset.Now;

            var headline = CreateHeadline(sourceName, title, url, publishedAt, summary: body, isLiveUpdate: true);
            if (headline is not null)
            {
                headlines.Add(headline);
            }
        }
    }

    private string? FindCurrentAlJazeeraLiveBlogUrl(Uri homepageUri, string homepageContent)
    {
        var candidates = _alJazeeraLiveBlogRegex.Matches(homepageContent)
            .Select(match => match.Groups["path"].Value)
            .Select(NormalizeUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => Uri.TryCreate(homepageUri, url, out var absoluteUri) ? absoluteUri.ToString() : null)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return candidates.FirstOrDefault();
    }

    private static string BuildAlJazeeraAmpUrl(string liveBlogUrl)
    {
        return liveBlogUrl.Replace("https://www.aljazeera.com/news/liveblog/", "https://www.aljazeera.com/amp/news/liveblog/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            _ => null
        };
    }

    private static NewsHeadline? CreateHeadline(
        string sourceName,
        string? title,
        string? link,
        DateTimeOffset publishedAt,
        string? summary = null,
        bool isLiveUpdate = false,
        bool isMock = false)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        return new NewsHeadline(
            sourceName,
            CollapseWhitespace(title),
            link,
            publishedAt,
            isMock,
            summary: string.IsNullOrWhiteSpace(summary) ? null : CollapseWhitespace(RemoveTags(WebUtility.HtmlDecode(summary))),
            isLiveUpdate: isLiveUpdate);
    }

    private static List<NewsHeadline> FilterAndNormalize(IEnumerable<NewsHeadline> headlines, IEnumerable<string> focusTerms)
    {
        var focusList = focusTerms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(term => term.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalized = headlines
            .Where(headline => headline.IsLiveUpdate || headline.PublishedAt >= DateTimeOffset.Now.AddDays(-7))
            .DistinctBy(headline => $"{headline.SourceName}:{headline.Title}")
            .OrderByDescending(headline => headline.PublishedAt)
            .ToList();

        if (focusList.Count == 0)
        {
            return normalized.Take(80).ToList();
        }

        var focused = normalized
            .Where(headline => headline.IsLiveUpdate || GetRelevanceScore(headline, focusList) > 0)
            .ToList();

        var cap = Math.Max(80, normalized.Count(headline => headline.IsLiveUpdate) + 24);
        return (focused.Count > 0 ? focused : normalized).Take(cap).ToList();
    }

    private static List<NewsHeadline> BuildFallbackHeadlines()
    {
        var now = DateTimeOffset.Now;

        return
        [
            new("Al Jazeera Live", "Liveblog updates are temporarily unavailable. The next refresh will try the current homepage live feed again.", "https://www.aljazeera.com/", now.AddMinutes(-2), true, isLiveUpdate: true),
            new("Bloomberg Politics", "Regional headlines are being filtered broadly for conflict escalation, diplomacy, oil, and Gulf security developments.", "https://feeds.bloomberg.com/politics/news.rss", now.AddMinutes(-4), true),
            new("BBC World", "Ticker is running with fallback data so the animation shell can still be tested while feeds recover.", "http://feeds.bbci.co.uk/news/world/rss.xml", now.AddMinutes(-6), true)
        ];
    }

    private static int GetRelevanceScore(NewsHeadline headline, IReadOnlyCollection<string> focusTerms)
    {
        var searchable = $"{headline.Title} {headline.Summary} {headline.Url}";
        var score = 0;

        foreach (var term in focusTerms)
        {
            if (searchable.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += term.Contains(' ', StringComparison.Ordinal) ? 2 : 1;
            }
        }

        return score;
    }

    private static string? CleanTrackedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var withoutFragment = url.Split('#', 2)[0];
        if (!Uri.TryCreate(withoutFragment, UriKind.Absolute, out var uri))
        {
            var parts = withoutFragment.Split('?', 2);
            if (parts.Length < 2)
            {
                return withoutFragment;
            }

            var relativeQuery = parts[1]
                .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(pair => pair.StartsWith("update=", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return relativeQuery.Length == 0
                ? parts[0]
                : $"{parts[0]}?{string.Join("&", relativeQuery)}";
        }

        var filteredQuery = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(pair => pair.StartsWith("update=", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var builder = new UriBuilder(uri)
        {
            Query = filteredQuery.Length == 0 ? string.Empty : string.Join("&", filteredQuery),
            Fragment = string.Empty
        };

        return builder.Uri.ToString();
    }

    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var withoutFragment = url.Split('#', 2)[0];

        if (!Uri.TryCreate(withoutFragment, UriKind.Absolute, out var uri))
        {
            return withoutFragment.Split('?', 2)[0];
        }

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty
        };

        return builder.Uri.ToString();
    }

    private static string RemoveTags(string input)
    {
        return Regex.Replace(input, "<.*?>", string.Empty);
    }

    private static string CollapseWhitespace(string input)
    {
        return Regex.Replace(input, "\\s+", " ").Trim();
    }

    private sealed record SourceReadResult(IReadOnlyList<NewsHeadline> Headlines, string? Error);
}
