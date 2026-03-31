using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class AcceptLanguageHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name     = "Accept-Language",
            In       = ParameterLocation.Header,
            Required = false,
            Schema   = new OpenApiSchema
            {
                Type    = "string",
                Default = new Microsoft.OpenApi.Any.OpenApiString("en")
            },
            Description = "Language preference for the response (e.g. en, ar)."
        });
    }
}
