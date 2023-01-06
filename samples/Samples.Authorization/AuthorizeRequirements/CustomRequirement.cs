using Microsoft.AspNetCore.Authorization;

namespace GraphQL.Server.Samples.Authorization.AuthorizeRequirements;

public class CustomRequirement : IAuthorizationRequirement
{
    public CustomRequirement()
    {

    }
}
