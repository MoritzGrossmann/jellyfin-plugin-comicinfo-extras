using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.ComicInfoExtras.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.ComicInfoExtras.Pages;

/// <summary>
/// Adds the page count script to the web client's index.html as it is served.
/// A server plugin cannot change the details page otherwise, and the files on disk stay untouched.
/// </summary>
public class WebScriptInjector : IStartupFilter
{
    private const string Marker = "comicinfo-extras-pagecount";

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(InjectAsync);
            next(app);
        };
    }

    /// <summary>
    /// Returns the path prefix (the server's base URL) if the request is for the web client's index.html.
    /// </summary>
    /// <param name="path">Request path.</param>
    /// <returns>The prefix, e.g. "" or "/jellyfin", or null for other requests.</returns>
    internal static string? GetIndexPrefix(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var suffix in new[] { "/web/index.html", "/web/" })
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return path[..^suffix.Length];
            }
        }

        return null;
    }

    /// <summary>
    /// Inserts the script tag before &lt;/head&gt;. Leaves the page alone if it is already there or has no head.
    /// </summary>
    /// <param name="html">The index.html content.</param>
    /// <param name="prefix">The server's base URL.</param>
    /// <returns>The changed page, or null if nothing was changed.</returns>
    internal static string? Inject(string html, string prefix)
    {
        if (html.Contains(Marker, StringComparison.Ordinal))
        {
            return null;
        }

        var head = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (head < 0)
        {
            return null;
        }

        var version = typeof(WebScriptInjector).Assembly.GetName().Version;
        var tag = $"<script id=\"{Marker}\" defer src=\"{prefix}/ComicInfoExtras/web/pageCount.js?v={version}\"></script>";
        return html.Insert(head, tag);
    }

    private static async Task InjectAsync(HttpContext context, Func<Task> next)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var prefix = config.ShowPageCount && HttpMethods.IsGet(context.Request.Method)
            ? GetIndexPrefix(context.Request.Path.Value)
            : null;

        if (prefix is null)
        {
            await next().ConfigureAwait(false);
            return;
        }

        // Get the plain, full page: no compression, no 304 from a cached copy without the script.
        context.Request.Headers.Remove(HeaderNames.AcceptEncoding);
        context.Request.Headers.Remove(HeaderNames.IfNoneMatch);
        context.Request.Headers.Remove(HeaderNames.IfModifiedSince);

        var original = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next().ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = original;
        }

        buffer.Position = 0;
        var isHtml = context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;
        if (context.Response.StatusCode == StatusCodes.Status200OK && isHtml)
        {
            var html = Encoding.UTF8.GetString(buffer.ToArray());
            var changed = Inject(html, prefix);
            if (changed is not null)
            {
                var bytes = Encoding.UTF8.GetBytes(changed);
                context.Response.ContentLength = bytes.Length;
                context.Response.Headers.Remove(HeaderNames.ETag);
                context.Response.Headers.Remove(HeaderNames.LastModified);
                await original.WriteAsync(bytes, context.RequestAborted).ConfigureAwait(false);
                return;
            }
        }

        await buffer.CopyToAsync(original, context.RequestAborted).ConfigureAwait(false);
    }
}
