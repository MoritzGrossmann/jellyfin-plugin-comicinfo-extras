using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ComicInfoExtras.Pages;

/// <summary>
/// Page count of books, and the web client script that shows it on the details page.
/// </summary>
[ApiController]
[Route("ComicInfoExtras")]
public class PageCountController : ControllerBase
{
    /// <summary>
    /// Resource name of the web client script.
    /// </summary>
    internal const string ScriptResource = "Jellyfin.Plugin.ComicInfoExtras.Pages.pageCount.js";

    private readonly ILibraryManager _libraryManager;
    private readonly IAuthorizationContext _authorizationContext;
    private readonly PageCountService _pageCounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="PageCountController"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="authorizationContext">Instance of the <see cref="IAuthorizationContext"/> interface.</param>
    /// <param name="pageCounts">The page count service.</param>
    public PageCountController(ILibraryManager libraryManager, IAuthorizationContext authorizationContext, PageCountService pageCounts)
    {
        _libraryManager = libraryManager;
        _authorizationContext = authorizationContext;
        _pageCounts = pageCounts;
    }

    /// <summary>
    /// Gets the page count of a book.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page count, or 404 if the item is not a book the user can see or its page count is unknown.</returns>
    [HttpGet("Items/{itemId}/PageCount")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageCountResult>> GetPageCount([FromRoute] Guid itemId, CancellationToken cancellationToken)
    {
        var auth = await _authorizationContext.GetAuthorizationInfo(Request).ConfigureAwait(false);

        Book? book;
        try
        {
            book = _libraryManager.GetItemById<Book>(itemId, auth.UserId);
        }
        catch (Exception)
        {
            // Unknown item, wrong type or no access: all look the same to the caller.
            book = null;
        }

        if (book is null)
        {
            return NotFound();
        }

        var pages = await _pageCounts.GetPageCountAsync(book, cancellationToken).ConfigureAwait(false);
        return pages is null ? NotFound() : new PageCountResult(pages.Value);
    }

    /// <summary>
    /// Gets the script that adds the page count to the details page. Loaded by a script tag, so without authentication.
    /// </summary>
    /// <returns>The script.</returns>
    [HttpGet("web/pageCount.js")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    public ActionResult GetScript()
    {
        var stream = typeof(PageCountController).Assembly.GetManifestResourceStream(ScriptResource);
        if (stream is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=3600";
        return File(stream, "application/javascript; charset=utf-8");
    }
}

/// <summary>
/// Page count of a book.
/// </summary>
/// <param name="PageCount">Number of pages.</param>
public sealed record PageCountResult(int PageCount);
