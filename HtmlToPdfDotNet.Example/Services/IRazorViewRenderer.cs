using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace HtmlToPdfDotNet.Example.Services;

/// <summary>
/// Contract for rendering a Razor view to an HTML string.
/// </summary>
public interface IRazorViewRenderer
{
    /// <summary>
    /// Renders the specified view with the given model and returns the resulting HTML string.
    /// </summary>
    /// <typeparam name="TModel">Type of the view model.</typeparam>
    /// <param name="viewName">Relative view name (e.g. "Report/SalesReport").</param>
    /// <param name="model">The model to pass to the view.</param>
    Task<string> RenderToStringAsync<TModel>(string viewName, TModel model);
}
