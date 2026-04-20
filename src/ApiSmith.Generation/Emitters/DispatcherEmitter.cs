using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

/// <summary>Emits the VSA dispatcher infrastructure (FR-20) with no third-party mediator dependency.</summary>
public static class DispatcherEmitter
{
    public static IEnumerable<EmittedFile> Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        var ns = layout.DispatcherNamespace(config);
        var content = $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Reflection;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{ns}};

            public interface IRequest<TResponse> { }

            public interface IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                Task<TResponse> HandleAsync(TRequest request, CancellationToken ct);
            }

            public interface IDispatcher
            {
                Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
            }

            public interface IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                Task<TResponse> HandleAsync(TRequest request, CancellationToken ct, Func<Task<TResponse>> next);
            }

            internal sealed class Dispatcher : IDispatcher
            {
                private readonly IServiceProvider _services;

                public Dispatcher(IServiceProvider services)
                {
                    _services = services;
                }

                public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
                {
                    var requestType = request.GetType();
                    var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
                    var handler = _services.GetRequiredService(handlerType);

                    var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
                    var behaviors = ((IEnumerable<object>)_services.GetServices(behaviorType)).ToList();

                    var handlerMethod = handlerType.GetMethod("HandleAsync")!;
                    Func<Task<TResponse>> next = () =>
                    {
                        var task = (Task<TResponse>)handlerMethod.Invoke(handler, new object[] { request, ct })!;
                        return task;
                    };

                    var behaviorMethod = behaviorType.GetMethod("HandleAsync")!;
                    for (var i = behaviors.Count - 1; i >= 0; i--)
                    {
                        var inner = next;
                        var behavior = behaviors[i];
                        next = () =>
                        {
                            var task = (Task<TResponse>)behaviorMethod.Invoke(behavior, new object[] { request, ct, inner })!;
                            return task;
                        };
                    }

                    return await next().ConfigureAwait(false);
                }
            }

            public static class DispatcherServiceCollectionExtensions
            {
                public static IServiceCollection AddDispatcher(this IServiceCollection services, Assembly assembly)
                {
                    services.AddScoped<IDispatcher, Dispatcher>();

                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsAbstract || type.IsInterface)
                        {
                            continue;
                        }

                        var interfaces = type.GetInterfaces()
                            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>));

                        foreach (var iface in interfaces)
                        {
                            services.AddScoped(iface, type);
                        }
                    }

                    return services;
                }
            }
            """;

        yield return new EmittedFile(layout.DispatcherPath(config), content);

        // Emit a companion LoggingBehavior<,> example so users have a concrete starting point
        // for IPipelineBehavior<,>. Registered by default in Program.cs — remove from DI to disable.
        var dispatcherPath = layout.DispatcherPath(config);
        var dispatcherDir = dispatcherPath[..dispatcherPath.LastIndexOf('/')];
        var loggingBehaviorPath = $"{dispatcherDir}/LoggingBehavior.cs";

        var loggingBehavior = $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.Logging;

            namespace {{ns}};

            /// <summary>Example pipeline behavior. Logs every request and response. Registered by default; remove from Program.cs to disable.</summary>
            public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _log;

                public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> log)
                {
                    _log = log;
                }

                public async Task<TResponse> HandleAsync(TRequest request, CancellationToken ct, Func<Task<TResponse>> next)
                {
                    _log.LogInformation("{Request} started", typeof(TRequest).Name);
                    var response = await next().ConfigureAwait(false);
                    _log.LogInformation("{Request} completed", typeof(TRequest).Name);
                    return response;
                }
            }
            """;

        yield return new EmittedFile(loggingBehaviorPath, loggingBehavior);
    }
}
