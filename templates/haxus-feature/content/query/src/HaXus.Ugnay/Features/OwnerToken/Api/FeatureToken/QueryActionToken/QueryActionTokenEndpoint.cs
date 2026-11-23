using HaXus.Ugnay.Features.OwnerToken.Application.FeatureToken.QueryActionToken;
using HaXus.Ugnay.Common.Interfaces.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HaXus.Ugnay.Features.OwnerToken.Api.FeatureToken.QueryActionToken;

public static class QueryActionTokenEndpoint
{
    // Route and authorization policy are explicit feature implementation choices.
    public static RouteHandlerBuilder Map(RouteGroupBuilder group, string pattern)
        => group.MapMethods(pattern, [HttpMethods.Get], HandleAsync);

    public static async ValueTask<Ok<QueryActionTokenResponse>> HandleAsync(
        [AsParameters] QueryActionTokenRequest request,
        IApplicationDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        // Add transport-to-application and result-to-response mappings with the feature's fields.
        _ = await dispatcher.DispatchAsync(new QueryActionTokenQuery(), cancellationToken);
        return TypedResults.Ok(new QueryActionTokenResponse());
    }
}
