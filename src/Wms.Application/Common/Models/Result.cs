using System.Text.Json.Serialization;

namespace Wms.Application.Common.Models;

public class Result
{
    public bool Succeeded { get; protected set; }
    public string? ErrorMessage { get; protected set; }
    public IReadOnlyList<string> Errors { get; protected set; } = Array.Empty<string>();

    [JsonConstructor]
    public Result(bool succeeded, string? errorMessage, IReadOnlyList<string>? errors = null)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
        if (errors != null)
        {
            Errors = errors.ToList();
        }
    }

    public static Result Success() => new(true, null);

    public static Result Failure(string errorMessage) => new(false, errorMessage, new[] { errorMessage });

    public static Result Failure(IEnumerable<string> errors)
    {
        var list = errors.ToList();
        return new Result(false, list.FirstOrDefault(), list);
    }
}

public class Result<T> : Result
{
    public T? Data { get; private set; }

    [JsonConstructor]
    public Result(bool succeeded, T? data, string? errorMessage, IReadOnlyList<string>? errors = null)
        : base(succeeded, errorMessage, errors)
    {
        Data = data;
    }

    public static Result<T> Success(T data) => new(true, data, null);

    public new static Result<T> Failure(string errorMessage) => new(false, default, errorMessage, new[] { errorMessage });

    public new static Result<T> Failure(IEnumerable<string> errors)
    {
        var list = errors.ToList();
        return new Result<T>(false, default, list.FirstOrDefault(), list);
    }
}
