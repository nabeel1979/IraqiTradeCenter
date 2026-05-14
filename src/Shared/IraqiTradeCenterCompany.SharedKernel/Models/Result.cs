namespace IraqiTradeCenterCompany.SharedKernel.Models;

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public List<string> Errors { get; } = new();

    protected Result(bool ok, string? err) { IsSuccess = ok; Error = err; if (err != null) Errors.Add(err); }

    public static Result Success() => new(true, null);
    public static Result Failure(string err) => new(false, err);
    public static Result<T> Success<T>(T val) => new(val, true, null);
    public static Result<T> Failure<T>(string err) => new(default, false, err);
}

public class Result<T> : Result
{
    public T? Value { get; }
    internal Result(T? val, bool ok, string? err) : base(ok, err) { Value = val; }
}
