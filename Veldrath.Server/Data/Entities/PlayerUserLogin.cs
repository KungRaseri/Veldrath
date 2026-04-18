using Microsoft.AspNetCore.Identity;

namespace Veldrath.Server.Data.Entities;

/// <summary>
/// Extends <see cref="IdentityUserLogin{TKey}"/> with a timestamp recording when the provider
/// was linked to the account. Maps to the <c>AspNetUserLogins</c> table.
/// </summary>
public class PlayerUserLogin : IdentityUserLogin<Guid>
{
    /// <summary>
    /// UTC timestamp when this OAuth provider was linked to the account.
    /// <c>null</c> for rows created before link-date tracking was introduced.
    /// </summary>
    public DateTimeOffset? LinkedAt { get; set; }
}
