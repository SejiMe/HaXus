using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using HaXus.Ugnay.Common.Interfaces.Application;
using HaXus.Ugnay.Features.Hanay.Api.Fleet.GetFleet;
using HaXus.Ugnay.Features.Hanay.Api.Fleet.CreateFleet;
using HaXus.Ugnay.Features.Hanay.Api.BindingGet.GetBindingGet;
using HaXus.Ugnay.Features.Hanay.Api.BindingPost.CreateBindingPost;
using HaXus.Ugnay.Features.Hanay.Api.BindingPut.ReplaceBindingPut;
using HaXus.Ugnay.Features.Hanay.Api.BindingPatch.UpdateBindingPatch;
using HaXus.Ugnay.Features.Hanay.Api.BindingDelete.DeleteBindingDelete;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using TemplateVerification;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Host.UseDefaultServiceProvider(options => { options.ValidateScopes = true; options.ValidateOnBuild = true; });
builder.Logging.ClearProviders();
builder.Services.AddScoped<ScopeProbe>();
builder.Services.AddSingleton<IApplicationDispatcher, FixtureDispatcher>();
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors = [typeof(ProbeBehavior<,>)];
});
await using var app = builder.Build();
var group = app.MapGroup("/checks");
GetFleetEndpoint.Map(group, "/stub-get");
CreateFleetEndpoint.Map(group, "/stub-post");
GetBindingGetEndpoint.Map(group, "/get");
GetBindingGetEndpoint.Map(group, "/get-route/{Probe}");
CreateBindingPostEndpoint.Map(group, "/post");
ReplaceBindingPutEndpoint.Map(group, "/put");
UpdateBindingPatchEndpoint.Map(group, "/patch");
DeleteBindingDeleteEndpoint.Map(group, "/delete");
DeleteBindingDeleteEndpoint.Map(group, "/delete-route/{Probe}");

var spy = new CancellationSpy();
using var cancellation = new CancellationTokenSource();
await GetFleetEndpoint.HandleAsync(new GetFleetRequest(), spy, cancellation.Token);
Check(spy.LastToken == cancellation.Token, "Query endpoint changed cancellation token");
await CreateFleetEndpoint.HandleAsync(new CreateFleetRequest(), spy, cancellation.Token);
Check(spy.LastToken == cancellation.Token, "Command endpoint changed cancellation token");

await app.StartAsync();
try
{
    using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    Check((await client.GetAsync("/checks/stub-get")).StatusCode == HttpStatusCode.InternalServerError,
        "Unimplemented query returned success");
    Check((await client.PostAsJsonAsync("/checks/stub-post", new { })).StatusCode == HttpStatusCode.InternalServerError,
        "Unimplemented command returned success");
    var dispatcher = app.Services.GetRequiredService<IApplicationDispatcher>();
    await Expect<NotImplementedException>(async () => await dispatcher.DispatchAsync(
        new HaXus.Ugnay.Features.Hanay.Application.Fleet.GetFleet.GetFleetQuery()));
    await Expect<NotImplementedException>(async () => await dispatcher.DispatchAsync(
        new HaXus.Ugnay.Features.Hanay.Application.Fleet.CreateFleet.CreateFleetCommand()));

    foreach (var (method, route, probe) in new[]
    {
        ("GET", "/checks/get?Probe=query", "query"),
        ("GET", "/checks/get-route/route", "route"),
        ("DELETE", "/checks/delete?Probe=delete-query", "delete-query"),
        ("DELETE", "/checks/delete-route/delete-route", "delete-route"),
        ("POST", "/checks/post", "post"), ("PUT", "/checks/put", "put"), ("PATCH", "/checks/patch", "patch")
    })
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (method is "POST" or "PUT" or "PATCH") request.Content = JsonContent.Create(new { Probe = probe });
        using var response = await client.SendAsync(request);
        Check(response.StatusCode == HttpStatusCode.OK, method + " did not bind/dispatch successfully");
        var value = await response.Content.ReadFromJsonAsync<ProbeResponse>();
        Check(value?.Probe == probe, method + " lost the bound value");
    }
    using (var missingBody = new HttpRequestMessage(HttpMethod.Post, "/checks/post"))
        Check((await client.SendAsync(missingBody)).StatusCode == HttpStatusCode.BadRequest, "Missing JSON body was accepted");
    using (var invalidBody = new HttpRequestMessage(HttpMethod.Post, "/checks/post"))
    {
        invalidBody.Content = new StringContent("{invalid", System.Text.Encoding.UTF8, "application/json");
        Check((await client.SendAsync(invalidBody)).StatusCode == HttpStatusCode.BadRequest, "Malformed JSON was accepted");
    }
    cancellation.Cancel();
    await Expect<OperationCanceledException>(async () => await dispatcher.DispatchAsync(
        new HaXus.Ugnay.Features.Hanay.Application.BindingGet.GetBindingGet.GetBindingGetQuery("cancel"), cancellation.Token));
    Check(ScopeProbe.Created.Count >= 12, "Expected operation scopes were not created");
    Check(ScopeProbe.Created.Count == ScopeProbe.Created.Distinct().Count(), "Operation scopes were reused");
    Check(ScopeProbe.Created.Order().SequenceEqual(ScopeProbe.Disposed.Order()), "Operation scopes were not asynchronously disposed");
    Console.WriteLine("PASS query/command stubs, query/route/JSON binding, pipeline scope, cancellation, and async disposal");
}
finally { await app.StopAsync(); }

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task Expect<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}

namespace TemplateVerification
{
    public sealed record ProbeResponse(string? Probe);

    public sealed class ScopeProbe : IAsyncDisposable
    {
        public static ConcurrentBag<Guid> Created { get; } = [];
        public static ConcurrentBag<Guid> Disposed { get; } = [];
        public Guid Id { get; } = Guid.NewGuid();
        public bool PipelineVisited { get; set; }
        public ScopeProbe() => Created.Add(Id);
        public ValueTask DisposeAsync() { Disposed.Add(Id); return ValueTask.CompletedTask; }
    }

    // Test-only adapter: production caller context and operation policy are deliberately absent.
    public sealed class FixtureDispatcher(IServiceScopeFactory scopes) : IApplicationDispatcher
    {
        public async ValueTask<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        {
            await using var scope = scopes.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(query, cancellationToken);
        }
        public async ValueTask<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        {
            await using var scope = scopes.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
        }
    }

    public sealed class ProbeBehavior<TMessage, TResult>(ScopeProbe scope) : IPipelineBehavior<TMessage, TResult>
        where TMessage : notnull, IMessage
    {
        public ValueTask<TResult> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResult> next, CancellationToken cancellationToken)
        {
            scope.PipelineVisited = true;
            return next(message, cancellationToken);
        }
    }

    public sealed class CancellationSpy : IApplicationDispatcher
    {
        public CancellationToken LastToken { get; private set; }
        public ValueTask<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
        { LastToken = cancellationToken; return ValueTask.FromResult(Activator.CreateInstance<TResult>()); }
        public ValueTask<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
        { LastToken = cancellationToken; return ValueTask.FromResult(Activator.CreateInstance<TResult>()); }
    }
}
