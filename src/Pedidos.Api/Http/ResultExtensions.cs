using Pedidos.Domain.Common;

namespace Pedidos.Api.Http;

/// <summary>Único ponto de tradução de <see cref="Result"/> para HTTP (RFC 7807).</summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : result.Errors.ToProblem();

    public static IResult ToCreatedResult<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? Results.Created(location(result.Value), result.Value) : result.Errors.ToProblem();

    public static IResult ToProblem(this IReadOnlyList<Error> errors)
    {
        // Entrada malformada tem prioridade: devolve todos os campos inválidos de uma vez.
        if (errors.Any(e => e.Type == ErrorType.Validacao))
        {
            var porCampo = errors
                .Where(e => e.Type == ErrorType.Validacao)
                .GroupBy(e => e.Campo ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());

            return Results.ValidationProblem(
                porCampo,
                title: "Requisição inválida.",
                extensions: new Dictionary<string, object?> { ["code"] = "validacao" });
        }

        var principal = errors[0];
        var status = principal.Type == ErrorType.NaoEncontrado
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status422UnprocessableEntity;

        return Results.Problem(
            statusCode: status,
            title: Titulo(status),
            detail: string.Join(" ", errors.Select(e => e.Message)),
            extensions: new Dictionary<string, object?>
            {
                ["code"] = principal.Code,
                ["erros"] = errors.Select(e => new { e.Code, e.Message }).ToArray(),
            });
    }

    private static string Titulo(int status) =>
        status == StatusCodes.Status404NotFound ? "Recurso não encontrado." : "Regra de negócio violada.";
}
