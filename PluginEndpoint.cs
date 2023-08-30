using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

public class PluginEndpoint
{
    private readonly ILogger _logger;

    public PluginEndpoint(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PluginEndpoint>();
    }

    [Function("WellKnownAIPlugin")]
    public async Task<HttpResponseData> WellKnownAIPlugin(
     [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".well-known/ai-plugin.json")] HttpRequestData req)
    {
        var toReturn = new AIPlugin();
        toReturn.Api.Url = $"{req.Url.Scheme}://{req.Url.Host}:{req.Url.Port}/swagger.json";

        var r = req.CreateResponse(HttpStatusCode.OK);
        await r.WriteAsJsonAsync(toReturn);
        return r;
    }

    [OpenApiOperation(operationId: "AppendToDocument", tags: new[] { "AppendToDocumentFunction" }, Description = "Appends the given text to an Azure Block Blob.")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AppendToDocRequest), Description = "JSON describing the content to append and the WriteableBlobUri.", Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "text/plain", bodyType: typeof(string), Description = "Confirms that the content was written.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    [Function("AppendToDocument")]
    public async Task<HttpResponseData> AppendToDocument([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "doc")] HttpRequestData req)
    {
        _logger.LogInformation("Beginning to append content to blob");

        var appendRequest = JsonSerializer.Deserialize<AppendToDocRequest>(req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (string.IsNullOrWhiteSpace(appendRequest!.WriteableBlobUri))
        {
            var r1 = req.CreateResponse(HttpStatusCode.BadRequest);
            await r1.WriteAsJsonAsync(new { error = "WriteableBlobUri is required." });
            return r1;
        }

        if (string.IsNullOrWhiteSpace(appendRequest.Content))
        {
            var r2 = req.CreateResponse(HttpStatusCode.BadRequest);
            await r2.WriteAsJsonAsync(new { error = "Content is required." });
            return r2;
        }

        await WordDocWriter.AppendContentToBlob(appendRequest.WriteableBlobUri, appendRequest.Content);

        _logger.LogInformation("Content was appended to blob");

        var r = req.CreateResponse(HttpStatusCode.Created);            
        return r;
    }
}
