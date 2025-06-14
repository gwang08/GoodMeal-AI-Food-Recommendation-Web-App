using SharedLibrary.Common.Messaging;

namespace Application.Auths.Commands;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string Name
) : ICommand;