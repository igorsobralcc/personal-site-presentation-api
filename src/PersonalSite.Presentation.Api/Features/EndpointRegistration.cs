using PersonalSite.Presentation.Api.Common;

namespace PersonalSite.Presentation.Api.Features;

public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapPresentationApi(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/v1/admin").AddEndpointFilter<AdminKeyFilter>();
        ProfileEndpoints.Map(admin);
        NamedResourceEndpoints.Map(admin);
        SkillEndpoints.Map(admin);
        ExperienceEndpoints.Map(admin);
        ProjectEndpoints.Map(admin);
        PublicPresentationEndpoints.Map(endpoints);
        return endpoints;
    }
}
