using Microsoft.Extensions.DependencyInjection;

namespace HtmlToPdfDotNet.Library.Infrastructure;

/// <summary>
/// Extension methods for registering HtmlToPdfDotNet with ASP.NET Core Dependency Injection.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds the HTML to PDF Generator services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> with the services added.</returns>
    public static IServiceCollection AddHtmlToPdfDotNet(this IServiceCollection services)
    {
        services.AddSingleton<IPdfGenerator, PdfGenerator>();
        return services;
    }
}