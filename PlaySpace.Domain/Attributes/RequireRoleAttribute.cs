using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace PlaySpace.Domain.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _requiredRoles;

    public RequireRoleAttribute(params string[] roles)
    {
        _requiredRoles = roles;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (_requiredRoles.Length == 0) return;

        var userRoles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        // Check if user has any of the required roles
        var hasRequiredRole = _requiredRoles.Any(role => userRoles.Contains(role));

        if (!hasRequiredRole)
        {
            context.Result = new ForbidResult($"Access denied. Required role(s): {string.Join(", ", _requiredRoles)}");
        }
    }
}

// Extension methods for easier role checking
public static class UserExtensions
{
    public static bool IsInRole(this ClaimsPrincipal user, string role)
    {
        return user.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == role);
    }

    public static bool IsInAnyRole(this ClaimsPrincipal user, params string[] roles)
    {
        var userRoles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        return roles.Any(role => userRoles.Contains(role));
    }

    public static List<string> GetRoles(this ClaimsPrincipal user)
    {
        return user.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();
    }
}