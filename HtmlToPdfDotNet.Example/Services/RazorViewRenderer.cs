using HtmlToPdfDotNet.Example.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace HtmlToPdfDotNet.Example.Services;

/// <summary>
/// Uses the ASP.NET Core Razor engine to render a view to an HTML string.
/// This allows the same Razor template to be re-used for both the browser
/// response and the PDF conversion.
/// </summary>
public class RazorViewRenderer(
    IRazorViewEngine viewEngine,
    ITempDataProvider tempDataProvider,
    IServiceProvider serviceProvider) : IRazorViewRenderer
{
    /// <inheritdoc/>
    public async Task<string> RenderToStringAsync<TModel>(string viewName, TModel model)
    {
        // Build a minimal HttpContext that the Razor engine requires.
        DefaultHttpContext httpContext = new() { RequestServices = serviceProvider };
        ActionContext actionContext = new(httpContext, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

        await using StringWriter sw = new();

        ViewEngineResult viewResult = viewEngine.FindView(actionContext, viewName, isMainPage: true);

        if (!viewResult.Success)
            throw new InvalidOperationException($"Razor view '{viewName}' not found. Searched locations: {string.Join(", ", viewResult.SearchedLocations ?? [])}");

        ViewDataDictionary<TModel> viewData = new(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model
        };

        TempDataDictionary tempData = new(httpContext, tempDataProvider);

        ViewContext viewContext = new(
            actionContext,
            viewResult.View,
            viewData,
            tempData,
            sw,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return sw.ToString();
    }
}
