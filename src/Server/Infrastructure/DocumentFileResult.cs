using GoGoaRentals.Services.Fleet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace GoGoaRentals.Server.Infrastructure;

public static class DocumentFileResult
{
    /// <summary>Serves an uploaded document inline (viewable in a browser tab) with a sensible file name for saving.</summary>
    public static IActionResult Inline(ControllerBase controller, DocumentFile file)
    {
        var headers = controller.Response.Headers;
        headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = file.DownloadName }.ToString();
        headers.XContentTypeOptions = "nosniff";
        headers.CacheControl = "private, no-store";
        return controller.File(file.Content, file.ContentType);
    }
}
