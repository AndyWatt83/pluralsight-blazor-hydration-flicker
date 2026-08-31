namespace FlickerDemo.Services;

/// <summary>
/// Stands in for "the API".
///
/// Two things make it useful on camera. It is deliberately slow, so the second
/// fetch is impossible to miss. And it records every call it receives, so the
/// page can show — as a fact, not a claim — that the same data was fetched
/// twice for a single page load.
/// </summary>
public class SlowApi
{
    /// <summary>
    /// Dial this for the recording. 1500ms makes the flicker last long enough
    /// to narrate over; drop it to ~400ms to show how easily it hides.
    /// </summary>
    public static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(1500);

    private readonly List<FetchRecord> _trace = [];
    private readonly Lock _gate = new();

    /// <summary>The fetches served for the page load currently on screen.</summary>
    public IReadOnlyList<FetchRecord> Trace
    {
        get { lock (_gate) return _trace.ToArray(); }
    }

    /// <summary>
    /// The caller passes in which renderer it is running under and which
    /// component instance it is, so the trace can answer the two questions the
    /// clip is really about: where did this run, and was it the same object?
    /// </summary>
    public async Task<Post> GetPostAsync(string renderer, bool isInteractive, string instanceId)
    {
        await Task.Delay(Latency);

        var post = new Post(
            "The server already fetched this",
            "This text was fetched on the server and written into the HTML your "
                + "browser first received. Watch the badge above to see what happens next.",
            DateTime.Now);

        lock (_gate)
        {
            // A non-interactive call means a fresh page load is prerendering,
            // so the trace starts over. That keeps the table on screen showing
            // exactly one page load's calls — no drift between takes, and no
            // stray request inflating the count mid-recording.
            if (!isInteractive) _trace.Clear();

            _trace.Add(new FetchRecord(
                _trace.Count + 1, renderer, isInteractive, instanceId, post.FetchedAt));
        }

        return post;
    }
}

public record Post(string Title, string Body, DateTime FetchedAt);

public record FetchRecord(
    int Number, string Renderer, bool IsInteractive, string InstanceId, DateTime At);
