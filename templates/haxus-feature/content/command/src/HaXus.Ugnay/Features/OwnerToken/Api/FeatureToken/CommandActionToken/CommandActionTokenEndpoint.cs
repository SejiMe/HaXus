using HaXus.Ugnay.Features.OwnerToken.Application.FeatureToken.CommandActionToken;
using HaXus.Ugnay.Common.Interfaces.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace HaXus.Ugnay.Features.OwnerToken.Api.FeatureToken.CommandActionToken;

public static class CommandActionTokenEndpoint
{
    // Route and authorization policy are explicit feature implementation choices.
    public static RouteHandlerBuilder Map(RouteGroupBuilder group, string pattern)
        => group.MapMethods(pattern, [HttpMethods.CommandMethodToken], HandleAsync);

    public static async ValueTask<Ok<CommandActionTokenResponse>> HandleAsync(
#if (commandMethod == "Delete")
        [AsParameters] CommandActionTokenRequest request,
#else
        [FromBody] CommandActionTokenRequest request,
#endif
        IApplicationDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        // Add transport-to-application and result-to-response mappings with the feature's fields.
        _ = await dispatcher.DispatchAsync(new CommandActionTokenCommand(), cancellationToken);
        return TypedResults.Ok(new CommandActionTokenResponse());
    }
}
