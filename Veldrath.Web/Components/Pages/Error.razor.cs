using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;

namespace Veldrath.Web.Components.Pages;

/// <summary>Code-behind for the error page displayed when an unhandled exception occurs.</summary>
public sealed partial class Error
{
    /// <summary>Gets or sets the HTTP context accessor for retrieving exception details.</summary>
    [Inject]
    private IHttpContextAccessor HttpContextAccessor { get; set; } = null!;

    /// <summary>Gets or sets the web host environment to determine development vs. production mode.</summary>
    [Inject]
    private IWebHostEnvironment Environment { get; set; } = null!;

    /// <summary>Gets or sets the logger for recording error details.</summary>
    [Inject]
    private ILogger<Error> Logger { get; set; } = null!;

    /// <summary>Gets the HTTP status code of the error.</summary>
    internal int StatusCode { get; private set; } = 500;

    /// <summary>Gets the reason phrase corresponding to <see cref="StatusCode"/>.</summary>
    internal string StatusCodeReason { get; private set; } = "Internal Server Error";

    /// <summary>Gets the original exception path, if available.</summary>
    internal string? OriginalPath { get; private set; }

    /// <summary>Gets the original query string, if available.</summary>
    internal string? OriginalQueryString { get; private set; }

    /// <summary>Gets a value indicating whether the application is running in the Development environment.</summary>
    internal bool IsDevelopment => Environment.IsDevelopment();

    /// <summary>Gets the exception details, if available in development mode.</summary>
    internal Exception? Exception { get; private set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        CaptureErrorDetails();
    }

    private void CaptureErrorDetails()
    {
        var ctx = HttpContextAccessor.HttpContext;
        if (ctx is null)
        {
            // During interactive (SignalR) rendering, HttpContext is unavailable.
            // Error details were already captured during the SSR prerender phase.
            return;
        }

        var exceptionFeature = ctx.Features.Get<IExceptionHandlerFeature>();
        var statusCodeFeature = ctx.Features.Get<IStatusCodeReExecuteFeature>();

        if (exceptionFeature is not null)
        {
            Exception = exceptionFeature.Error;
            Logger.LogError(Exception, "Unhandled exception occurred while processing request.");

            StatusCode = ctx.Response.StatusCode;
            StatusCodeReason = ReasonPhrases.GetReasonPhrase(StatusCode) ?? "Error";
        }
        else
        {
            // No exception feature — likely a non-exception status code redirect.
            StatusCode = ctx.Response.StatusCode;
            StatusCodeReason = ReasonPhrases.GetReasonPhrase(StatusCode) ?? "Error";
        }

        if (statusCodeFeature is not null)
        {
            OriginalPath = statusCodeFeature.OriginalPath;
            OriginalQueryString = statusCodeFeature.OriginalQueryString;
        }
    }
}
