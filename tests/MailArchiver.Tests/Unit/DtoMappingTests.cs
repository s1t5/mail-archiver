using MailArchiver.Models;
using MailArchiver.Models.Api;
using MailArchiver.Models.ViewModels;

namespace MailArchiver.Tests.Unit;

public class DtoMappingTests
{
    [Fact]
    public void MailAccountDto_DoesNotExposeCredentialProperties()
    {
        var dtoType = typeof(MailAccountDto);
        var forbiddenProperties = new[] { "Password", "ClientSecret", "Username", "ClientId", "TenantId" };

        foreach (var propertyName in forbiddenProperties)
        {
            Assert.Null(dtoType.GetProperty(propertyName));
        }
    }

    [Fact]
    public void MailAccountDto_FromEntity_MapsSafeFields()
    {
        var lastSync = new DateTime(2026, 6, 11, 8, 15, 0, DateTimeKind.Utc);
        var account = new MailAccount
        {
            Id = 42,
            Name = "Support Mailbox",
            EmailAddress = "support@example.com",
            Provider = ProviderType.IMAP,
            IsEnabled = true,
            LastSync = lastSync
        };

        var dto = MailAccountDto.FromEntity(account);

        Assert.Equal(account.Id, dto.Id);
        Assert.Equal(account.Name, dto.Name);
        Assert.Equal(account.EmailAddress, dto.EmailAddress);
        Assert.Equal("IMAP", dto.Provider);
        Assert.Equal(account.IsEnabled, dto.IsEnabled);
        Assert.Equal(account.LastSync, dto.LastSync);
    }

    [Fact]
    public void FolderNodeDto_FromNode_MapsNestedTree()
    {
        var node = new FolderTreeNode
        {
            Name = "INBOX",
            FullPath = "INBOX",
            TotalCount = 10,
            Level = 0,
            Children =
            {
                new FolderTreeNode
                {
                    Name = "Work",
                    FullPath = "INBOX/Work",
                    TotalCount = 4,
                    Level = 1
                }
            }
        };

        var dto = FolderNodeDto.FromNode(node);

        Assert.Equal("INBOX", dto.Name);
        Assert.Equal("INBOX", dto.FullPath);
        Assert.Equal(10, dto.TotalCount);
        Assert.Equal(0, dto.Level);
        Assert.Single(dto.Children);
        Assert.Equal("Work", dto.Children[0].Name);
        Assert.Equal("INBOX/Work", dto.Children[0].FullPath);
        Assert.Equal(4, dto.Children[0].TotalCount);
        Assert.Equal(1, dto.Children[0].Level);
    }
}
