namespace TrustRent.Modules.Admin.Models;

public class AdminRole
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; } = false;

    public ICollection<AdminRolePermission> Permissions { get; set; } = new List<AdminRolePermission>();
    public ICollection<AdminUserRole> Members { get; set; } = new List<AdminUserRole>();
}

public class AdminRolePermission
{
    public Guid RoleId { get; set; }
    public AdminRole? Role { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public Permission? Permission { get; set; }
}

public class AdminUserRole
{
    public Guid AdminUserId { get; set; }
    public AdminUser? AdminUser { get; set; }
    public Guid RoleId { get; set; }
    public AdminRole? Role { get; set; }
}

public class AdminUserPermission
{
    public Guid AdminUserId { get; set; }
    public AdminUser? AdminUser { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public Permission? Permission { get; set; }
    public bool IsGrant { get; set; } = true;
}
