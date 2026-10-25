namespace Veldrath.Server.Infrastructure.Email;

/// <summary>
/// Reduces an e-mail address to a form that is safe to write to application logs.
/// </summary>
/// <remarks>
/// Addresses are personal data: the log sinks include a rolling file, so writing the full
/// address would persist it in clear text (CodeQL cs/cleartext-storage). Keeping the first
/// character and the domain keeps support conversations useful without identifying the mailbox.
/// </remarks>
public static class EmailAddressMask
{
    /// <summary>
    /// Masks the local part of <paramref name="address"/>, for example
    /// <c>j***@example.com</c>.
    /// </summary>
    /// <param name="address">The address to mask; may be <see langword="null"/> or blank.</param>
    /// <returns>
    /// The masked address, or <c>(none)</c> when no address was supplied.
    /// </returns>
    public static string Mask(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "(none)";

        var at = address.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at == address.Length - 1)
            return "***";

        return $"{address[0]}***{address[at..]}";
    }
}
