using Mediator;

namespace HaXus.Ugnay.Features.OwnerToken.Application.FeatureToken.CommandActionToken;

public sealed class CommandActionTokenHandler : ICommandHandler<CommandActionTokenCommand, CommandActionTokenResult>
{
    public ValueTask<CommandActionTokenResult> Handle(CommandActionTokenCommand message, CancellationToken cancellationToken)
        => throw new NotImplementedException("Implement CommandActionToken before registering its endpoint.");
}
