using FluentValidation;
using MediatR;

namespace WebApiMediatorCQRS.Commands;

public record PingCommand : IRequest<PingCommandResponse>
{
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PingCommandValidator : AbstractValidator<PingCommand>
{
    public PingCommandValidator()
    {
        RuleFor(x => x.Message).NotEmpty().Must(IsValidMessage);
    }

    private bool IsValidMessage(string message) =>
        !string.IsNullOrWhiteSpace(message) && message.Length >= 3;
}

public record PingCommandResponse(string Message);

public class PingCommandHandler : IRequestHandler<PingCommand, PingCommandResponse>
{
    public Task<PingCommandResponse> Handle(
        PingCommand request,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(new PingCommandResponse(request.Message));
    }
}
