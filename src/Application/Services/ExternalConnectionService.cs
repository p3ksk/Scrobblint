using Microsoft.Extensions.Logging;
using Scrobblint.Application.Abstractions;
using Scrobblint.Application.Abstractions.Persistence;
using Scrobblint.Application.Abstractions.Relay;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;
using Scrobblint.Domain.Enums;
using Scrobblint.Shared.Connections;
using Scrobblint.Shared.Relay;

namespace Scrobblint.Application.Services;

public sealed class ExternalConnectionService : IExternalConnectionService
{
    private readonly IExternalConnectionRepository _connections;
    private readonly IListenBrainzRelay _listenBrainz;
    private readonly ILastfmRelay _lastfm;
    private readonly IFailedRelayRepository _failedRelays;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<ExternalConnectionService> _logger;

    public ExternalConnectionService(
        IExternalConnectionRepository connections,
        IListenBrainzRelay listenBrainz,
        ILastfmRelay lastfm,
        IFailedRelayRepository failedRelays,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ExternalConnectionService> logger)
    {
        _connections = connections;
        _listenBrainz = listenBrainz;
        _lastfm = lastfm;
        _failedRelays = failedRelays;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<ConnectionsResponse>> GetConnectionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _connections.GetByUserAsync(userId, cancellationToken);
        var dtos = items
            .Select(c => new ExternalConnectionDto(c.Provider, c.IsEnabled, c.ExternalUsername, c.ApiRoot))
            .ToList();

        return Result<ConnectionsResponse>.Ok(new ConnectionsResponse(
            dtos, _listenBrainz.IsConfigured, _lastfm.IsConfigured));
    }

    public async Task<Result> ConnectListenBrainzAsync(Guid userId, ConnectListenBrainzRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Result.Invalid(new Dictionary<string, string[]> { ["token"] = new[] { "A ListenBrainz token is required." } });

        var apiRoot = string.IsNullOrWhiteSpace(request.ApiRoot) ? null : request.ApiRoot.Trim().TrimEnd('/');

        var validation = await _listenBrainz.ValidateTokenAsync(request.Token.Trim(), apiRoot, cancellationToken);
        if (!validation.Success)
            return Result.Fail(ResultError.Validation, validation.Error ?? "Could not validate the ListenBrainz token.");

        await UpsertAsync(userId, ScrobbleProvider.ListenBrainz, validation.Credential!, apiRoot, validation.Username, cancellationToken);
        _logger.LogInformation("User {UserId} connected ListenBrainz ({Account})", userId, validation.Username);
        return Result.Ok();
    }

    public Result<string> BeginLastfmAuth(string callbackUrl)
    {
        if (!_lastfm.IsConfigured)
            return Result<string>.Fail(ResultError.Validation, "Last.fm is not configured on this server.");
        if (string.IsNullOrWhiteSpace(callbackUrl))
            return Result<string>.Invalid(new Dictionary<string, string[]> { ["callbackUrl"] = new[] { "A callback URL is required." } });

        return Result<string>.Ok(_lastfm.BuildAuthorizeUrl(callbackUrl));
    }

    public async Task<Result> CompleteLastfmAuthAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        if (!_lastfm.IsConfigured)
            return Result.Fail(ResultError.Validation, "Last.fm is not configured on this server.");
        if (string.IsNullOrWhiteSpace(token))
            return Result.Invalid(new Dictionary<string, string[]> { ["token"] = new[] { "Missing Last.fm token." } });

        var auth = await _lastfm.CompleteAuthorizationAsync(token.Trim(), cancellationToken);
        if (!auth.Success)
            return Result.Fail(ResultError.Validation, auth.Error ?? "Could not complete Last.fm authorization.");

        await UpsertAsync(userId, ScrobbleProvider.Lastfm, auth.Credential!, null, auth.Username, cancellationToken);
        _logger.LogInformation("User {UserId} connected Last.fm ({Account})", userId, auth.Username);
        return Result.Ok();
    }

    public async Task<Result> SetEnabledAsync(Guid userId, ScrobbleProvider provider, bool enabled, CancellationToken cancellationToken = default)
    {
        var connection = await _connections.GetAsync(userId, provider, cancellationToken);
        if (connection is null)
            return Result.NotFound("No such connection.");

        connection.IsEnabled = enabled;
        _connections.Update(connection);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> DisconnectAsync(Guid userId, ScrobbleProvider provider, CancellationToken cancellationToken = default)
    {
        var connection = await _connections.GetAsync(userId, provider, cancellationToken);
        if (connection is null)
            return Result.NotFound("No such connection.");

        _connections.Remove(connection);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} disconnected {Provider}", userId, provider);
        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<UserFailedRelayDto>>> GetFailedRelaysAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _failedRelays.GetByUserIdAsync(userId, 50, cancellationToken);
        var dtos = items
            .Select(r => new UserFailedRelayDto(
                r.Id, r.Provider, r.Status, Mappers.CountRelayTracks(r.TracksJson),
                r.RetryCount, r.LastError, Mappers.ToUnix(r.UpdatedAt)))
            .ToList();

        return Result<IReadOnlyList<UserFailedRelayDto>>.Ok(dtos);
    }

    public async Task<Result> RetryFailedRelayAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var failedRelay = await _failedRelays.GetByIdAsync(id, cancellationToken);
        if (failedRelay is null || failedRelay.UserId != userId)
            return Result.NotFound("Retry cache record not found.");

        failedRelay.Status = RelayStatus.Pending;
        failedRelay.RetryCount = 0;
        failedRelay.NextRetryAt = DateTime.UtcNow;
        failedRelay.UpdatedAt = DateTime.UtcNow;
        _failedRelays.Update(failedRelay);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }

    public async Task<Result> DeleteFailedRelayAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var failedRelay = await _failedRelays.GetByIdAsync(id, cancellationToken);
        if (failedRelay is null || failedRelay.UserId != userId)
            return Result.NotFound("Retry cache record not found.");

        _failedRelays.Remove(failedRelay);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }

    private async Task UpsertAsync(Guid userId, ScrobbleProvider provider, string credential, string? apiRoot, string? username, CancellationToken cancellationToken)
    {
        var existing = await _connections.GetAsync(userId, provider, cancellationToken);
        if (existing is null)
        {
            await _connections.AddAsync(new ExternalConnection
            {
                UserId = userId,
                Provider = provider,
                IsEnabled = true,
                Token = credential,
                ApiRoot = apiRoot,
                ExternalUsername = username,
                CreatedAt = _clock.UtcNow
            }, cancellationToken);
        }
        else
        {
            existing.Token = credential;
            existing.ApiRoot = apiRoot;
            existing.ExternalUsername = username;
            existing.IsEnabled = true;
            _connections.Update(existing);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
