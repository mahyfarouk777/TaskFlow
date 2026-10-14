
using Microsoft.AspNetCore.Identity;

namespace TaskFlow.Data;

public static class IdentitySeed
{
    public static async Task EnsureRolesAsync(
        IServiceProvider services)
    {
        var roleManager =
            services.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roles =
        [
            "Admin",
            "ProjectManager",
            "TeamMember"
        ];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(
                    new IdentityRole(role));

                if (!result.Succeeded)
                {
                    var errors = string.Join(
                        "; ",
                        result.Errors.Select(e => e.Description));

                    throw new InvalidOperationException(
                        $"Could not create role {role}: {errors}");
                }
            }
        }
    }
}
