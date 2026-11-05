// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Clients;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Provides an implementation of <see cref="ITenantPromotionContributor"/> that promotes the grants of the source
/// tenant's clients that the host does not manage. A grant is matched by its client and the resource server the
/// promotion maps for it, and the host-managed clients' grants stay out.
/// </summary>
// See ADR-1425.
internal class ClientGrantPromotionContributor(
    IEnumerable<IHostManagedClientPolicy> hostManagedClientPolicies
) : ITenantPromotionContributor
{
    private const int PageLimit = 100;

    private IReadOnlyList<IHostManagedClientPolicy> HostManagedClientPolicies { get; } =
    [.. hostManagedClientPolicies];

    /// <inheritdoc />
    public int Order => TenantPromotionOrders.ClientGrants;

    /// <inheritdoc />
    public async ValueTask PlanAsync(
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        var promotions = await ComputeAsync(context, cancellationToken);

        foreach (var promotion in promotions)
        {
            foreach (var detail in promotion.Errors)
            {
                context.AddError(
                    new TenantPromotionErrorResource
                    {
                        ResourceType = ManagementResourceTypes.ClientGrant,
                        ResourceId = promotion.ResourceId,
                        Detail = detail,
                    }
                );
            }

            if (promotion.TargetResourceServerId is null)
            {
                continue;
            }

            context.AddChange(
                new TenantPromotionChangeResource
                {
                    ResourceType = ManagementResourceTypes.ClientGrant,
                    ResourceId = promotion.ResourceId,
                    Action = promotion.Action,
                }
            );
        }
    }

    /// <inheritdoc />
    public async ValueTask ApplyAsync(
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        var promotions = await ComputeAsync(context, cancellationToken);
        var store = context.StoreManager.GetStore<IClientGrantStore>();

        foreach (var promotion in promotions)
        {
            if (
                promotion.Action == TenantPromotionActions.Unchanged
                || promotion.TargetResourceServerId is not { } targetResourceServerId
            )
            {
                continue;
            }

            using var scope = context.BeginTargetScope();

            if (promotion.Target is null)
            {
                await store.AddAsync(
                    new PersistedClientGrant
                    {
                        TenantId = context.TargetTenant.TenantId,
                        ClientId = promotion.ClientId,
                        ResourceServerId = targetResourceServerId,
                        ConcurrencyToken = string.Empty,
                        Scopes = [.. promotion.Source.Scopes],
                    },
                    cancellationToken
                );
            }
            else
            {
                promotion.Target.Scopes = [.. promotion.Source.Scopes];
                await store.UpdateAsync(promotion.Target, cancellationToken);
            }
        }
    }

    private bool IsHostManaged(string tenantId, string clientId) =>
        HostManagedClientPolicies.Any(policy => policy.IsHostManaged(tenantId, clientId));

    private async ValueTask<List<ClientGrantPromotion>> ComputeAsync(
        TenantPromotionContext context,
        CancellationToken cancellationToken
    )
    {
        var clientStore = context.StoreManager.GetStore<IClientStore>();
        var grantStore = context.StoreManager.GetStore<IClientGrantStore>();
        var resourceServerStore = context.StoreManager.GetStore<IResourceServerStore>();

        var sources = await GetSourceGrantsAsync(
            context,
            clientStore,
            grantStore,
            cancellationToken
        );
        var promotions = new List<ClientGrantPromotion>();

        foreach (var (clientId, grant) in sources)
        {
            var errors = new List<string>();

            if (
                !context.TryGetTargetIdentifier(
                    ManagementResourceTypes.ResourceServer,
                    grant.ResourceServerId,
                    out var targetResourceServerId
                )
            )
            {
                errors.Add(
                    $"The resource server '{grant.ResourceServerId}' the grant reaches is neither promoted nor a system resource server of the target tenant."
                );

                promotions.Add(
                    new ClientGrantPromotion
                    {
                        ClientId = clientId,
                        Source = grant,
                        Target = null,
                        TargetResourceServerId = null,
                        Action = TenantPromotionActions.Unchanged,
                        Errors = errors,
                    }
                );
                continue;
            }

            using var scope = context.BeginTargetScope();

            var target = await grantStore.GetOrDefaultAsync(
                clientId,
                targetResourceServerId,
                cancellationToken
            );

            var targetResourceServer = await resourceServerStore.GetOrDefaultAsync(
                targetResourceServerId,
                cancellationToken
            );
            if (targetResourceServer is { IsSystem: true })
            {
                var definedScopes = targetResourceServer
                    .Scopes.Select(x => x.Value)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var scopeValue in grant.Scopes.Where(x => !definedScopes.Contains(x)))
                {
                    errors.Add(
                        $"The target tenant's system resource server '{targetResourceServer.Identifier}' does not define the scope '{scopeValue}'."
                    );
                }
            }

            string action;
            if (target is null)
            {
                action = TenantPromotionActions.Created;
            }
            else if (
                new HashSet<string>(grant.Scopes, StringComparer.Ordinal).SetEquals(target.Scopes)
            )
            {
                action = TenantPromotionActions.Unchanged;
            }
            else
            {
                action = TenantPromotionActions.Updated;
            }

            promotions.Add(
                new ClientGrantPromotion
                {
                    ClientId = clientId,
                    Source = grant,
                    Target = target,
                    TargetResourceServerId = targetResourceServerId,
                    Action = action,
                    Errors = errors,
                }
            );
        }

        return promotions;
    }

    private async ValueTask<
        List<(string ClientId, PersistedClientGrant Grant)>
    > GetSourceGrantsAsync(
        TenantPromotionContext context,
        IClientStore clientStore,
        IClientGrantStore grantStore,
        CancellationToken cancellationToken
    )
    {
        using var scope = context.BeginSourceScope();

        var grants = new List<(string ClientId, PersistedClientGrant Grant)>();
        string? clientCursor = null;
        do
        {
            var clientPage = await clientStore.GetPageAsync(
                new PageRequest { Cursor = clientCursor, Limit = PageLimit },
                cancellationToken
            );

            foreach (var client in clientPage.Items)
            {
                if (
                    IsHostManaged(context.SourceTenant.TenantId, client.ClientId)
                    || IsHostManaged(context.TargetTenant.TenantId, client.ClientId)
                )
                {
                    continue;
                }

                string? grantCursor = null;
                do
                {
                    var grantPage = await grantStore.GetPageAsync(
                        client.ClientId,
                        new PageRequest { Cursor = grantCursor, Limit = PageLimit },
                        cancellationToken
                    );

                    foreach (var grant in grantPage.Items)
                    {
                        grants.Add((client.ClientId, grant));
                    }

                    grantCursor = grantPage.NextCursor;
                } while (grantCursor is not null);
            }

            clientCursor = clientPage.NextCursor;
        } while (clientCursor is not null);

        return grants;
    }
}
