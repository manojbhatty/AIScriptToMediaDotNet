using AIScriptToMediaDotNet.Agents.Base;
using Microsoft.Extensions.DependencyInjection;

namespace AIScriptToMediaDotNet.Agents.Extensions;

/// <summary>
/// Extension methods for registering agents with dependency injection.
/// </summary>
public static class AgentServiceCollectionExtensions
{
    /// <summary>
    /// Adds an agent to the service collection.
    /// </summary>
    /// <typeparam name="TAgent">The agent type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgent<TAgent>(this IServiceCollection services)
        where TAgent : BaseAgent
    {
        services.AddScoped<TAgent>();
        return services;
    }

    /// <summary>
    /// Adds multiple agents to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="agentTypes">The agent types to add.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgents(this IServiceCollection services, params Type[] agentTypes)
    {
        foreach (var agentType in agentTypes)
        {
            if (agentType.IsSubclassOf(typeof(BaseAgent)))
            {
                services.AddScoped(agentType);
            }
        }
        return services;
    }

    /// <summary>
    /// Adds multiple agents to the service collection.
    /// </summary>
    /// <typeparam name="TAgent1">The first agent type.</typeparam>
    /// <typeparam name="TAgent2">The second agent type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgents<TAgent1, TAgent2>(this IServiceCollection services)
        where TAgent1 : BaseAgent
        where TAgent2 : BaseAgent
    {
        services.AddScoped<TAgent1>();
        services.AddScoped<TAgent2>();
        return services;
    }

    /// <summary>
    /// Adds multiple agents to the service collection.
    /// </summary>
    /// <typeparam name="TAgent1">The first agent type.</typeparam>
    /// <typeparam name="TAgent2">The second agent type.</typeparam>
    /// <typeparam name="TAgent3">The third agent type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgents<TAgent1, TAgent2, TAgent3>(this IServiceCollection services)
        where TAgent1 : BaseAgent
        where TAgent2 : BaseAgent
        where TAgent3 : BaseAgent
    {
        services.AddScoped<TAgent1>();
        services.AddScoped<TAgent2>();
        services.AddScoped<TAgent3>();
        return services;
    }
}
