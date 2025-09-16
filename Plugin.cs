using System.Text.Json;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace JellyRay;

public class JellyRayPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public PluginConfiguration Configuration { get; set; } = new PluginConfiguration();

    private readonly ISessionManager _sessionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IEncodingManager _encodingManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly IFileSystem _fileSystem;

    public override string Name => "JellyRay";
    public override Guid Id => Guid.Parse("C2EAC02A-9691-402A-A537-AE3583E6B02B");

    public JellyRayPlugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ISessionManager sessionManager,
        ILibraryManager libraryManager,
        IEncodingManager encodingManager,
        IMediaEncoder mediaEncoder,
        IFileSystem fileSystem,
        IHttpClientFactory httpClientFactory) : base(applicationPaths, xmlSerializer)
    {
        _sessionManager = sessionManager;
        _libraryManager = libraryManager;
        _encodingManager = encodingManager;
        _mediaEncoder = mediaEncoder;
        _fileSystem = fileSystem;
        _httpClientFactory = httpClientFactory;

        _sessionManager.PlaybackStopped += OnPlaybackStopped;
    }

    private async void OnPlaybackStopped(object? sender, PlaybackProgressEventArgs e)
    {
        if (!e.IsPaused)
            return;

        var item = _libraryManager.GetItemById(e.Item.Id);
        if (item is not Video video)
            return;

        long ticks = e.PlaybackPositionTicks ?? 0;
        double seconds = TimeSpan.FromTicks(ticks).TotalSeconds;

        int frameCount = Configuration.NumFrames;
        double window = Configuration.FrameWindowSeconds;
        var timestamps = Enumerable.Range(0, frameCount)
            .Select(i => seconds - window / 2 + (i * (window / (frameCount - 1))))
            .Where(t => t > 0)
            .ToList();

        // Extract frames using FFmpeg
        var extractor = new FrameExtractor(_mediaEncoder, _fileSystem);
        var frameFiles = new List<string>();
        foreach (var ts in timestamps)
        {
            string framePath = Path.Combine(Path.GetTempPath(), $"{video.Id}_{ts}.jpg");
            var path = await extractor.ExtractFrame(video, TimeSpan.FromSeconds(ts), framePath, CancellationToken.None);
            frameFiles.Add(path);
        }

        // Send frames to Python server
        var client = _httpClientFactory.CreateClient();
        var content = new MultipartFormDataContent();
        foreach (var frame in frameFiles)
        {
            content.Add(new ByteArrayContent(File.ReadAllBytes(frame)), "files", Path.GetFileName(frame));
        }

        var response = await client.PostAsync(Configuration.RecognizerApiUrl + "/recognize_batch", content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        var results = JsonSerializer.Deserialize<RecognitionBatchResult>(json);

        // Store results in DB
        foreach (var (frame, matches) in results.Results)
        {
            foreach (var match in matches)
            {
                SaveFaceRecognitionResult(video.Id, frame, match.Match, match.Score, match.Bbox);
            }
        }

        // Clean up temp files
        foreach (var frame in frameFiles)
        {
            try
            {
                File.Delete(frame);
            }
            catch { }
        }
    }

    private void SaveFaceRecognitionResult(Guid videoId, string frameFile, string personName, double score, int[] bbox)
    {
        // Implement database saving logic here
        // This is a placeholder for demonstration purposes
        Console.WriteLine($"VideoID: {videoId}, Frame: {frameFile}, Person: {personName}, Score: {score}, BBox: [{string.Join(",", bbox)}]");
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new List<PluginPageInfo>
    {
        new PluginPageInfo
        {
            Name = "jellyray",
            EmbeddedResourcePath = GetType().Namespace + ".Web.jellyray.html"
        }
    };
    }
}

public class PluginConfiguration : BasePluginConfiguration
{
    public int NumFrames { get; set; } = 5;
    public double FrameWindowSeconds { get; set; } = 5.0;
    public string RecognizerApiUrl { get; set; } = "http://localhost:5000";
}

public class RecognitionBatchResult
{
    public Dictionary<string, List<FaceMatch>> Results { get; set; }
}

public class FaceMatch
{
    public int[] Bbox { get; set; }
    public string Match { get; set; }
    public double Score { get; set; }
}