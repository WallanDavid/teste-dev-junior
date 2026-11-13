using Microsoft.AspNetCore.Diagnostics;

namespace Pedidos.Api.Http;

/// <summary>
/// Falhas de binding (ex.: <c>?dataInicio=abc</c> ou JSON malformado) lançam <see cref="BadHttpRequestException"/>
/// em Development. Sem este handler o ExceptionHandler as transformaria em 500.
/// </summary>
internal sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not BadHttpRequestException bad)
            return false;

        context.Response.StatusCode = bad.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails =
            {
                Status = bad.StatusCode,
                Title = "Requisição inválida.",
                Detail = bad.Message,
                Extensions = { ["code"] = "validacao" },
            },
        });
    }
}
