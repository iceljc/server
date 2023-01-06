using Microsoft.AspNetCore.Authorization;

namespace GraphQL.Server.Samples.Authorization.AuthorizeRequirements;

public class CustomRequirementHandler : AuthorizationHandler<CustomRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CustomRequirement requirement)
    {
        if (context.User?.Identity?.Name == "jlu")
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
        context.Fail();
        return Task.CompletedTask;
    }
}
