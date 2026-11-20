using KeyRemap.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace KeyRemap.Api.Controllers;

internal static class MappingProblems
{
    public static ObjectResult Invalid(IReadOnlyList<ValidationError> errors)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "One or more mappings are invalid.",
            Status = StatusCodes.Status400BadRequest,
        };
        problem.Extensions["errors"] = errors;

        var result = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    public static ObjectResult InvalidBody() =>
        Invalid([
            new ValidationError(null, "body", ValidationError.InvalidBody,
                "The body must be a JSON object with a mappings array of entries with integer source and target."),
        ]);
}
