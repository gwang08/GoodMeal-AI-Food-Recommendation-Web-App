using System.Security.Claims;
using FluentValidation;
using Microsoft.Extensions.Logging;
using SharedLibrary.Common.Messaging;
using SharedLibrary.Common.ResponseModel;
using Domain.Repositories;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using SharedLibrary.Common;

namespace Application.Business.Commands.EnableBusinessCommand;

public sealed record EnableBusinessCommand(Guid BusinessId) : ICommand<EnableBusinessResponse>;

public sealed record EnableBusinessResponse(
    Guid Id,
    string? OwnerId,
    string Name,
    bool IsDisable,
    DateTime? DisableAt,
    string? DisableBy
);

internal sealed class EnableBusinessCommandHandler : ICommandHandler<EnableBusinessCommand, EnableBusinessResponse>
{
    private readonly ILogger<EnableBusinessCommandHandler> _logger;
    private readonly IBusinessRepository _businessRepository;
    private readonly IMapper _mapper;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EnableBusinessCommandHandler(
        ILogger<EnableBusinessCommandHandler> logger,
        IBusinessRepository businessRepository,
        IMapper mapper, 
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _businessRepository = businessRepository;
        _mapper = mapper;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<EnableBusinessResponse>> Handle(EnableBusinessCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim is null)
            {
                _logger.LogWarning("User ID not found in claims");
                return Result.Failure<EnableBusinessResponse>(new Error("Auth.Unauthorized",
                    "User not authenticated"));
            }

            var userId = userIdClaim.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User context is missing for business enable");
                return Result.Failure<EnableBusinessResponse>(new Error("Authorization.Failed",
                    "User context is required"));
            }

            var business = await _businessRepository.GetByIdAsync(request.BusinessId);
            if (business == null)
            {
                _logger.LogWarning("Business with ID {BusinessId} not found", request.BusinessId);
                return Result.Failure<EnableBusinessResponse>(new Error("Business.NotFound", "Business not found"));
            }

            // Kiểm tra quyền sở hữu
            if (business.OwnerId != userId)
            {
                _logger.LogWarning("User {UserId} is not authorized to enable business {BusinessId}", userId,
                    request.BusinessId);
                return Result.Failure<EnableBusinessResponse>(new Error("Authorization.Failed",
                    "You are not authorized to enable this business"));
            }

            if (business.IsDisable != true)
            {
                _logger.LogWarning("Business {BusinessId} is already enabled", request.BusinessId);
                return Result.Failure<EnableBusinessResponse>(new Error("Business.AlreadyEnabled",
                    "Business is already enabled"));
            }

            business.IsDisable = false;
            business.DisableAt = null;
            business.DisableBy = null;
            business.UpdatedAt = DateTime.Now;

            _businessRepository.Update(business);

            var response = _mapper.Map<EnableBusinessResponse>(business);

            _logger.LogInformation("Successfully enabled business {BusinessId} by user {UserId}", request.BusinessId,
                userId);
            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error enabling business {BusinessId}", request.BusinessId);
            return Result.Failure<EnableBusinessResponse>(new Error("InternalError", "An unexpected error occurred"));
        }
    }
}

public class EnableBusinessCommandValidator : AbstractValidator<EnableBusinessCommand>
{
    public EnableBusinessCommandValidator()
    {
        RuleFor(x => x.BusinessId)
            .NotEmpty()
            .WithMessage("Business ID is required");
    }
} 