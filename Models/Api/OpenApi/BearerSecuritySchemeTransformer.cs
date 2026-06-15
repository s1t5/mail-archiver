using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MailArchiver.Models.Api.OpenApi
{
    /// <summary>
    /// Adds the bearer (API key) security scheme to the generated OpenAPI document
    /// and applies it as a global requirement, so the spec documents that every
    /// endpoint expects <c>Authorization: Bearer ma_...</c>.
    /// </summary>
    public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        public const string SchemeId = "Bearer";

        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            var scheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                Description = "Per-user API key. Send as: Authorization: Bearer ma_..."
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SchemeId] = scheme;

            var requirement = new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeId, document)] = new List<string>()
            };
            document.Security ??= new List<OpenApiSecurityRequirement>();
            document.Security.Add(requirement);

            return Task.CompletedTask;
        }
    }
}
