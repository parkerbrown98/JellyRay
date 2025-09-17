using System.Net.Mime;
using System.Reflection;
using JellyRay.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JellyRay.Api;

[ApiController]
[Route("JellyRay")]
public class FaceRecognitionController(FaceProcessingService faceService) : ControllerBase
{
    [HttpGet("faces")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces(MediaTypeNames.Application.Json)]
    public ActionResult GetFaces([FromQuery] Guid itemId, [FromQuery] long ticks, [FromQuery] int paddingSeconds = 5)
    {
        var results = faceService.GetResults(itemId, ticks, paddingSeconds);

        return Ok(new
        {
            faces = results.Select(r => new
            {
                name = r.Celebrity,
                confidence = r.Confidence
            })
        });
    }

    [HttpGet("jellyray.js")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("application/javascript")]
    public ActionResult GetPluginScript()
    {
        Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("JellyRay.Web.jellyray.js");

        if (stream == null)
        {
            return NotFound();
        }

        // SetCacheHeaders();

        return File(stream, "application/javascript");
    }

    [HttpGet("jellyray.css")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Produces("text/css")]
    public ActionResult GetPluginStylesheet()
    {
        Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("JellyRay.Web.jellyray.css");

        if (stream == null)
        {
            return NotFound();
        }

        // SetCacheHeaders();

        return File(stream, "text/css");
    }
}