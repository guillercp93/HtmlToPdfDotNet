using Microsoft.Extensions.DependencyInjection;

namespace HtmlToPdfDotNet.Library.Infrastructure;

public static class Extensions
{
    public static IServiceCollection AddHtmlToPdfDotNet(this IServiceCollection services)
    {
        services.AddSingleton<IPdfGenerator, PdfGenerator>();
        return services;
    }
}