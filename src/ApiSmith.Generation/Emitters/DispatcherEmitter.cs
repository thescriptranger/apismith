using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

/// <summary>Emits the VSA dispatcher infrastructure (FR-20) with no third-party mediator dependency.</summary>
public static class DispatcherEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        var ns = layout.DispatcherNamespace(config);
        var content = $$"""
            using System.Reflection;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{ns}};

            public interface IRequest<TResponse> { }

            public interface IRequestHandler<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                System.Threading.Tasks.Task<TResponse> HandleAsync(TRequest request, System.Threading.CancellationToken ct);
            }

            public interface IDispatcher
            {
                System.Threading.Tasks.Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, System.Threading.CancellationToken ct = default);
            }

            public interface IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest<TResponse>
            {
                System.Threading.Tasks.Task<TResponse> HandleAsync(
                    TRequest request,
                    System.Func<System.Threading.Tasks.Task<TResponse>> next,
                    System.Threading.CancellationToken ct);
            }

            internal sealed class Dispatcher : IDispatcher
            {
                private readonly IServiceProvider _services;

                public Dispatcher(IServiceProvider services)
                {
                    _services = services;
                }

                public System.Threading.Tasks.Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, System.Threading.CancellationToken ct = default)
                {
                    var requestType = request.GetType();
                    var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
                    var handler = _services.GetRequiredService(handlerType);
                    var method = handlerType.GetMethod("HandleAsync")!;
                    return (System.Threading.Tasks.Task<TResponse>)method.Invoke(handler, new object[] { request, ct })!;
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

        return new EmittedFile(layout.DispatcherPath(config), content);
    }
}
