using WebApi.Configs.Models;

namespace WebApi;

public static class ResultExtensionsApi
{
    public static IResult MapResult(this Result result)
    {
        if (result.IsSucess)
        {
            return Results.Ok();
        }

        return GetErrorResult(result.Error);
    }

    public static IResult MapResult<T>(this Result<T> result)
    {
        if (result.IsSucess)
        {
            return Results.Ok(result.Value);
        }

        return GetErrorResult(result.Error);
    }

    public static IResult MapResultCreated<T>(this Result<T> result)
    {
        if (result.IsSucess)
        {
            return Results.Created(string.Empty, result.Value);
        }

        return GetErrorResult(result.Error);
    }

    private static IResult GetErrorResult(Error? error)
    {
        if (error == null)
            return Results.BadRequest(ApiResultError.Create("Ação não foi concluida com sucesso!"));

        return error.GetType() switch
        {
            TypeError.Validation => Results.UnprocessableEntity(ApiResultError.Create(error)),
            TypeError.NotFound => Results.NotFound(ApiResultError.Create(error)),
            TypeError.Exception => Results.InternalServerError(ApiResultError.Create(error)),
            _ => throw new Exception("TypeErro invalid")
        };
    }
}
