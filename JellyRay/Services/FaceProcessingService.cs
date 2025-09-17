using System.Text.Json;
using JellyRay.Entities;
using JellyRay.Utils;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JellyRay.Services;

public class FaceProcessingService : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<FaceProcessingService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServerConfigurationManager _config;
    private readonly IHttpClientFactory _httpClientFactory;

    private FaceRecognitionDbContext? _dbContext;
    private string DbPath => Path.Join(_config.ApplicationPaths.DataPath, "jellyray.db");

    public FaceProcessingService(
        ISessionManager sessionManager,
        ILibraryManager libraryManager,
        IMediaEncoder mediaEncoder,
        IFileSystem fileSystem,
        ILogger<FaceProcessingService> logger,
        ILoggerFactory loggerFactory,
        IServerConfigurationManager config,
        IHttpClientFactory httpClientFactory)
    {
        _sessionManager = sessionManager;
        _libraryManager = libraryManager;
        _mediaEncoder = mediaEncoder;
        _fileSystem = fileSystem;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<FaceProcessingService>();
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    private async void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
    {
        try
        {
            if (e.Item == null || e.Item.Id == Guid.Empty || e.PlaybackPositionTicks == null || !e.IsPaused)
                return;

            var item = _libraryManager.GetItemById(e.Item.Id);
            if (item is not Video video)
                return;

            long ticks = e.PlaybackPositionTicks ?? 0;
            double seconds = TimeSpan.FromTicks(ticks).TotalSeconds;

            int frameCount = 5; // default value
            double window = 10.0; // default value

            if (Plugin.Instance?.Configuration != null)
            {
                frameCount = Plugin.Instance.Configuration.NumFrames;
                window = Plugin.Instance.Configuration.FrameWindowSeconds;
            }

            var timestamps = Enumerable.Range(0, frameCount)
                .Select(i => seconds - window / 2 + (i * (window / (frameCount - 1))))
                .Where(t => t > 0)
                .ToList();

            // Extract frames using FFmpeg
            var extractor = new FrameExtractor(_mediaEncoder, _fileSystem);
            var frameFiles = new List<string>();
            foreach (var ts in timestamps)
            {
                string framePath = Path.Combine(_config.ApplicationPaths.TempDirectory, $"{video.Id}_{ts}.jpg");
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

            var url = Plugin.Instance?.Configuration.RecognizerApiUrl ?? "http://localhost:5000";
            _logger.LogInformation($"Sending {frameFiles.Count} frames to {url}/recognize_batch");
            var response = await client.PostAsync(url + "/recognize_batch", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            var results = JsonSerializer.Deserialize<RecognitionBatchResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Save results to DB
            if (results != null && !await HasResults(e.Item.Id, ticks))
            {
                var allMatches = results.Results.Values.SelectMany(m => m);
                await SaveResults(e.Item.Id, ticks, allMatches);
            }

            // Clean up temp files
            foreach (var frame in frameFiles)
            {
                try
                {
                    _fileSystem.DeleteFile(frame);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing facial data.");
        }
    }

    private async Task<bool> HasResults(Guid itemId, long timestampTicks)
    {
        if (_dbContext == null)
        {
            throw new InvalidOperationException("Database context is not initialized.");
        }

        return await _dbContext.Results.AnyAsync(r => r.ItemId == itemId &&
            r.TimestampTicks >= timestampTicks - 0.1 * 10_000_000 &&
            r.TimestampTicks <= timestampTicks + 0.1 * 10_000_000);
    }

    private async Task SaveResults(Guid itemId, long timestampTicks, IEnumerable<FaceMatch> matches)
    {
        if (_dbContext == null)
        {
            throw new InvalidOperationException("Database context is not initialized.");
        }

        foreach (var match in matches)
        {
            var entity = new FaceRecognitionResult
            {
                ItemId = itemId,
                TimestampTicks = timestampTicks,
                Celebrity = match.Match,
                Confidence = match.Score,
                Bbox = string.Join(",", match.Bbox)
            };

            _dbContext.Results.Add(entity);
        }

        await _dbContext.SaveChangesAsync();
    }

    public IEnumerable<FaceRecognitionResult> GetResults(Guid itemId, long timestampTicks, int paddingSeconds = 1)
    {
        if (_dbContext == null)
        {
            throw new InvalidOperationException("Database context is not initialized.");
        }

        return _dbContext.Results
            .Where(r => r.ItemId == itemId &&
                r.TimestampTicks >= timestampTicks - paddingSeconds * 10_000_000 &&
                r.TimestampTicks <= timestampTicks + paddingSeconds * 10_000_000)
            .OrderByDescending(r => r.Confidence)
            .GroupBy(r => r.Celebrity)
            .Select(g => g.First())
            .ToList();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FaceProcessingService starting.");

        _dbContext = new FaceRecognitionDbContext(DbPath);
        _dbContext.Database.EnsureCreated();

        _sessionManager.PlaybackProgress += OnPlaybackProgress;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FaceProcessingService stopping.");

        _sessionManager.PlaybackProgress -= OnPlaybackProgress;

        _dbContext?.Dispose();
        _dbContext = null;

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _dbContext = null;
    }

    ~FaceProcessingService()
    {
        Dispose();
    }
}

public class RecognitionBatchResult
{
    public Dictionary<string, List<FaceMatch>> Results { get; set; } = new();
}

public class FaceMatch
{
    public int[] Bbox { get; set; } = Array.Empty<int>();
    public string Match { get; set; } = string.Empty;
    public double Score { get; set; }
}