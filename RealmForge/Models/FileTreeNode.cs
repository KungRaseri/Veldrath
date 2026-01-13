namespace RealmForge.Models;

/// <summary>
/// Node in the file tree hierarchy
/// </summary>
public class FileTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string Icon { get; set; } = string.Empty;
    public List<FileTreeNode> Children { get; set; } = new();
    public bool IsExpanded { get; set; } = false;
    
    /// <summary>
    /// Get relative display path from root
    /// </summary>
    public string GetRelativePath(string rootPath)
    {
        if (FullPath.StartsWith(rootPath))
            return FullPath.Substring(rootPath.Length).TrimStart('\\', '/');
        return FullPath;
    }
}
