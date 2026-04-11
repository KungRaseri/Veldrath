namespace Veldrath.Server.Data.Entities;

/// <summary>
/// A single server-wide key/value counter row.
/// Used for aggregates such as the global rescue fund total.
/// </summary>
public class GlobalStat
{
    /// <summary>Unique identifier for the stat (e.g. <c>"rescue_fund_total"</c>).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Current numeric value of the stat.</summary>
    public long Value { get; set; }
}
