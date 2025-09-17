using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Entities;

namespace JellyRay.Utils;

public class FrameExtractor
{
  private readonly IMediaEncoder _mediaEncoder;
  private readonly IFileSystem _fileSystem;

  public FrameExtractor(IMediaEncoder mediaEncoder, IFileSystem fileSystem)
  {
    _mediaEncoder = mediaEncoder;
    _fileSystem = fileSystem;
  }

  public async Task<string> ExtractFrame(Video video, TimeSpan offset, string outputPath, CancellationToken cancellationToken)
  {
    var inputPath = video.Path;

    // Ensure output path exists
    if (!_fileSystem.DirectoryExists(outputPath))
    {
      Directory.CreateDirectory(outputPath);
    }

    // Extract frame
    var output = await _mediaEncoder.ExtractVideoImage(inputPath, video.Container, video.GetMediaSources(false).First(), video.GetDefaultVideoStream(), null, offset, cancellationToken);

    return output;
  }
}