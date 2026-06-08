namespace ThortspaceApiStarter;

/// <summary>
/// A source of reference content for a topic — turned into a Thortspace sphere by <c>Program</c>.
/// Pluggable so you can swap where the material comes from. Ships with <see cref="WikipediaContentSource"/>
/// (reliable, works from any HTTP client) and a <see cref="GrokipediaContentSource"/> stub.
/// </summary>
public interface IContentSource
{
    Task<TopicPage> FetchAsync(string topic);
}

/// <summary>A fetched topic: a title, a one-paragraph summary, and a handful of sections.</summary>
public sealed record TopicPage(string Title, string Summary, IReadOnlyList<TopicSection> Sections);

/// <summary>One section of a topic: a heading (becomes a group label) and a few short points (become thorts).</summary>
public sealed record TopicSection(string Heading, IReadOnlyList<string> Thorts);
