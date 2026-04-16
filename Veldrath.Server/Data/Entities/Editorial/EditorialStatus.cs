namespace Veldrath.Server.Data.Entities.Editorial;

/// <summary>Publication lifecycle state for editorial content.</summary>
public enum EditorialStatus
{
    /// <summary>Content is unpublished and not visible to the public.</summary>
    Draft,

    /// <summary>Content is published and visible to the public.</summary>
    Published
}
