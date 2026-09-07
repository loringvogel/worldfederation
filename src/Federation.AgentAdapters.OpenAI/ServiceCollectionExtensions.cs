using Federation.AgentGateway;
using Microsoft.Extensions.DependencyInjection;

namespace Federation.AgentAdapters.OpenAI;

/// <summary>Registration extensions for the OpenAI agent adapter.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the OpenAI agent adapter.</summary>
    public static IServiceCollection AddOpenAiAdapter(this IServiceCollection services, Action<OpenAiAdapterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new OpenAiAdapterOptions { ApiKey = string.Empty };
        configure(options);

        services.AddSingleton(options);
        services.AddHttpClient<IAgentAdapter, OpenAiAgentAdapter>();

        return services;
    }
}
