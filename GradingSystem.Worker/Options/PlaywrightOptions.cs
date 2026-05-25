namespace GradingSystem.Worker.Options;

/// <summary>
/// Optional remote Chromium (Docker). When <see cref="BrowserCdpEndpoint"/> is set, Worker connects
/// over CDP instead of launching a local browser. Use <see cref="ArtifactHostFromBrowser"/> so the
/// container can reach student APIs bound on the host (e.g. host.docker.internal).
/// </summary>
public class PlaywrightOptions
{
    /// <summary>CDP URL, e.g. http://127.0.0.1:9222 (port published from docker-compose dev).</summary>
    public string? BrowserCdpEndpoint { get; set; }

    /// <summary>Host name student apps listen on, as seen from the browser container.</summary>
    public string ArtifactHostFromBrowser { get; set; } = "host.docker.internal";
}
