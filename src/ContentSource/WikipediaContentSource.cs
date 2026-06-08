using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThortspaceApiStarter;

/// <summary>
/// Default content source: the MediaWiki "extracts" API (en.wikipedia.org/w/api.php). One request returns the
/// whole article as plain text with <c>== Heading ==</c> section markers, from any HTTP client (no bot-blocking),
/// so the starter runs out-of-the-box. We take the lead as an "Introduction" group, then the first few top-level
/// sections, reducing each to a handful of short sentences (good thort-sized chunks).
/// </summary>
public sealed class WikipediaContentSource : IContentSource
{
    private const int MaxSections = 6;           // including the synthetic "Introduction"
    private const int MaxThortsPerSection = 4;
    private static readonly Regex HeaderLine = new(@"^(={2,})\s*(.+?)\s*={2,}\s*$", RegexOptions.Compiled);
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("ThortspaceApiStarter/1.0 (+https://github.com/gooisoft/thortspace-api-starter)");
        return c;
    }

    public async Task<TopicPage> FetchAsync(string topic)
    {
        var enc = Uri.EscapeDataString(topic);
        var url = "https://en.wikipedia.org/w/api.php?action=query&format=json&prop=extracts&explaintext=1" +
                  $"&exsectionformat=wiki&redirects=1&titles={enc}";
        var json = await Http.GetStringAsync(url);

        using var doc = JsonDocument.Parse(json);
        var pages = doc.RootElement.GetProperty("query").GetProperty("pages");
        JsonElement page = default;
        foreach (var p in pages.EnumerateObject()) { page = p.Value; break; }
        if (page.ValueKind != JsonValueKind.Object || !page.TryGetProperty("extract", out var exEl))
            throw new InvalidOperationException($"No Wikipedia article found for \"{topic}\".");
        var title = page.TryGetProperty("title", out var t) ? t.GetString() ?? topic : topic;
        var extract = exEl.GetString() ?? "";

        var sections = ParseSections(extract);
        if (sections.Count == 0)
            throw new InvalidOperationException($"No usable content for topic \"{topic}\" (title \"{title}\").");
        return new TopicPage(title, sections[0].Thorts.Count > 0 ? string.Join(" ", sections[0].Thorts) : "", sections);
    }

    // Split the plain-text article into sections by its '== Heading ==' markers. Text before the first heading is
    // the lead ("Introduction"). Level-3+ sub-headings are flattened into their parent section.
    private static List<TopicSection> ParseSections(string extract)
    {
        var sections = new List<TopicSection>();
        var heading = "Introduction";
        var body = new StringBuilder();

        void Flush()
        {
            if (sections.Count < MaxSections && body.Length > 0 && !IsBoilerplate(heading))
            {
                var thorts = SplitSentences(body.ToString()).Take(MaxThortsPerSection).ToList();
                if (thorts.Count > 0) sections.Add(new TopicSection(heading, thorts));
            }
            body.Clear();
        }

        foreach (var line in extract.Replace("\r\n", "\n").Split('\n'))
        {
            var m = HeaderLine.Match(line.Trim());
            if (m.Success)
            {
                if (m.Groups[1].Value.Length == 2) { Flush(); heading = m.Groups[2].Value.Trim(); }
                continue;                                    // level 3+ heading: flatten (drop the marker line)
            }
            body.AppendLine(line);
        }
        Flush();
        return sections;
    }

    private static bool IsBoilerplate(string heading) =>
        Regex.IsMatch(heading, @"^(See also|References|Notes|Footnotes|Further reading|External links|Bibliography|Citations|Sources|Gallery|Explanatory notes)$",
            RegexOptions.IgnoreCase);

    private static IEnumerable<string> SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        text = Regex.Replace(text, @"\s+", " ").Trim();
        foreach (var raw in Regex.Split(text, @"(?<=[.!?])\s+(?=[A-Z0-9])"))
        {
            var s = Regex.Replace(raw, @"\[\d+\]", "").Trim();   // drop [1]-style citation markers
            if (s.Length < 15) continue;                          // skip fragments
            if (s.Length > 90) s = s[..87].TrimEnd() + "…";  // thorts are short ideas
            yield return s;
        }
    }
}
