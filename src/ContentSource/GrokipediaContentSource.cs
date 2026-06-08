namespace ThortspaceApiStarter;

/// <summary>
/// STUB. Grokipedia (grokipedia.com) currently sits behind Cloudflare-style bot protection and returns
/// HTTP 403 to a plain HTTP fetch (no browser / JS challenge), so a bare <see cref="HttpClient"/> is rejected.
/// This adapter is left unimplemented on purpose. If you have access (or Grokipedia later exposes an API/feed),
/// implement <see cref="FetchAsync"/> the same shape as <see cref="WikipediaContentSource"/> — fetch the page
/// for the topic, strip it to a title + sections of short sentences, and return a <see cref="TopicPage"/>.
/// To use it, set <c>Program.Source</c> to <c>new GrokipediaContentSource()</c>.
/// </summary>
public sealed class GrokipediaContentSource : IContentSource
{
    public Task<TopicPage> FetchAsync(string topic) =>
        throw new NotSupportedException(
            "GrokipediaContentSource is a stub: grokipedia.com 403-blocks plain HTTP fetches (Cloudflare bot " +
            "protection). Use WikipediaContentSource (the default), or implement this adapter with your own " +
            "access/headers — mirror WikipediaContentSource's parsing.");
}
