using System.Diagnostics.CodeAnalysis;

namespace BranchBooking.Application.Common;

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) =>
        new(default, error ?? throw new ArgumentNullException(nameof(error)));
}
