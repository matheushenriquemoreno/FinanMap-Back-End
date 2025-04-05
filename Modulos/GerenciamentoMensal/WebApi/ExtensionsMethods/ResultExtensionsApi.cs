using System.Net;
using Microsoft.AspNetCore.Http.HttpResults;
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


    public static IResult MapResult(this List<Result> result)
    {
        if (result.All(x => x.IsSucess))
        {
            return Results.Ok();
        }

        if (result.All(x => x.IsFailure))
        {
            return Results.BadRequest(ApiResultError.Create(result.Select(x => x.Error.Message).ToList()));
        }

        MultiStatusResponse multiStatusResponse = new()
        {
            Errors = result.Select(x => x.Error.Message).ToList(),
            QuantidadeErros = result.Count(x => x.IsFailure),
            QuantidadeSucesso = result.Count(x => x.IsFailure)
        };

        return TypedResults.Json(multiStatusResponse, statusCode: (int)HttpStatusCode.MultiStatus);
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
