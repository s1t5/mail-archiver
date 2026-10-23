using MailArchiver.Models.ViewModels;

namespace MailArchiver.Models.Api;

public class FolderNodeDto
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int Level { get; set; }
    public List<FolderNodeDto> Children { get; set; } = new();

    public static FolderNodeDto FromNode(FolderTreeNode n)
    {
        return new FolderNodeDto
        {
            Name = n.Name,
            FullPath = n.FullPath,
            TotalCount = n.TotalCount,
            Level = n.Level,
            Children = n.Children.Select(FromNode).ToList()
        };
    }
}
