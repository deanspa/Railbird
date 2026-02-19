using System;
using System.Collections.Generic;

namespace Railbird.Core.Common;

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }

    private Result(bool isSuccess, T? value, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public static Result<T> Success(T value) => new(true, value, Array.Empty<string>());

    public static Result<T> Failure(IEnumerable<string> errors)
    {
        var list = errors?.ToList() ?? new List<string> { "Unknown error" };
        if (list.Count == 0)
        {
            list.Add("Unknown error");
        }

        return new Result<T>(false, default, list);
    }
}
