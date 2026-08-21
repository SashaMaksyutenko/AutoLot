using Microsoft.AspNetCore.Identity;

namespace AutoLot.Domain.Identity;

public class Role : IdentityRole<long>
{
    public Role()
    {
    }

    public Role(string roleName)
        : base(roleName)
    {
    }
}
