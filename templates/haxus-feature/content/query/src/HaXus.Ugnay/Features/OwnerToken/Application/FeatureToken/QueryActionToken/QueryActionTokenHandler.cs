using Mediator;

namespace HaXus.Ugnay.Features.OwnerToken.Application.FeatureToken.QueryActionToken;

public sealed class QueryActionTokenHandler : IQueryHandler<QueryActionTokenQuery, QueryActionTokenResult>
{
    public ValueTask<QueryActionTokenResult> Handle(QueryActionTokenQuery message, CancellationToken cancellationToken)
        => throw new NotImplementedException("Implement QueryActionToken before registering its endpoint.");
}
