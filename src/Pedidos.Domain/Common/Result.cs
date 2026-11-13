namespace Pedidos.Domain.Common;

/// <summary>Categoria do erro. A camada Api traduz cada categoria para um status HTTP.</summary>
public enum ErrorType
{
    /// <summary>Entrada malformada (400).</summary>
    Validacao,

    /// <summary>Recurso da URL não existe (404).</summary>
    NaoEncontrado,

    /// <summary>Entrada válida, mas viola regra de negócio (422).</summary>
    Regra,

    /// <summary>Conflito de concorrência (409).</summary>
    Conflito,
}

/// <param name="Code">Código estável em snake_case, consumido pelo frontend.</param>
/// <param name="Message">Mensagem legível para o usuário.</param>
/// <param name="Campo">Campo da requisição relacionado (para erros de validação).</param>
public sealed record Error(string Code, string Message, ErrorType Type, string? Campo = null);

public class Result
{
    protected Result(IReadOnlyList<Error> errors) => Errors = errors;

    public IReadOnlyList<Error> Errors { get; }
    public bool IsSuccess => Errors.Count == 0;
    public bool IsFailure => !IsSuccess;

    public static Result Success() => new([]);
    public static Result Failure(params Error[] errors) => Failure((IReadOnlyList<Error>)errors);

    public static Result Failure(IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0) throw new ArgumentException("Uma falha precisa de pelo menos um erro.", nameof(errors));
        return new Result(errors);
    }

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base([]) => _value = value;
    private Result(IReadOnlyList<Error> errors) : base(errors) { }

    /// <summary>Valor em caso de sucesso. Acessar em caso de falha é erro de programação.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não é possível acessar o valor de um resultado com falha.");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(params Error[] errors) => Failure((IReadOnlyList<Error>)errors);

    public static new Result<T> Failure(IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0) throw new ArgumentException("Uma falha precisa de pelo menos um erro.", nameof(errors));
        return new Result<T>(errors);
    }

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
