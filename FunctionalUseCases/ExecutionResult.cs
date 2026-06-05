namespace FunctionalUseCases;

public record ExecutionResult<T>(ExecutionError? Error = null) : ExecutionResult(Error)
    where T : notnull

{
    protected T? Value { get; set; }

    public override bool ExecutionSucceeded => this.Error is null && this.Value is not null;

    public override bool ExecutionFailed => this.Error is not null || this.Value is null;

    public T CheckedValue => this.GetValueOrThrow();

    public T GetValueOrThrow(string? exceptionMessage = null)
    {
        if (this.ExecutionSucceeded)
        {
            return this.Value!;
        }

        throw this.CreateExecutionException(exceptionMessage);
    }

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<ExecutionError, TResult> onFailure) =>
        this.ExecutionSucceeded
            ? onSuccess(this.Value!)
            : onFailure(this.Error ?? new ExecutionError("Unknown Error"));

    public ExecutionResult<TResult> Map<TResult>(Func<T, TResult> map)
        where TResult : notnull =>
        this.ExecutionSucceeded
            ? Execution.Success(map(this.Value!))
            : Execution.Failure<TResult>(this);

    public ExecutionResult<TResult> Bind<TResult>(Func<T, ExecutionResult<TResult>> bind)
        where TResult : notnull =>
        this.ExecutionSucceeded
            ? bind(this.Value!)
            : Execution.Failure<TResult>(this);

    public static implicit operator ExecutionResult<T>(T value) => new() { Value = value };

    public override string ToString() =>
        this.ExecutionSucceeded ? nameof(this.ExecutionSucceeded) : nameof(this.ExecutionFailed) + ": " + this.Error;

    public static ExecutionResult operator +(ExecutionResult<T> left, ExecutionResult right) =>
        Execution.Combine(left, right);
}

public record ExecutionResult(ExecutionError? Error = null)
{
    public bool? NoLog { get; internal set; }

    public virtual bool ExecutionSucceeded => this.Error is null;

    public virtual bool ExecutionFailed => this.Error is not null;

    public ExecutionError CheckedError => this.Error ?? throw new NullReferenceException();

    public override string ToString() =>
        this.ExecutionSucceeded ? nameof(this.ExecutionSucceeded) : nameof(this.ExecutionFailed) + ": " + this.Error;

    public void ThrowIfFailed(string? exceptionMessage = null)
    {
        if (!this.ExecutionFailed)
        {
            return;
        }

        throw this.CreateExecutionException(exceptionMessage);
    }

    protected ExecutionException CreateExecutionException(string? exceptionMessage = null)
    {
        var internalMessage = this.Error?.Message ?? "Unknown Error";
        var message = exceptionMessage is null
            ? internalMessage
            : exceptionMessage + ": " + internalMessage;

        return new ExecutionException(message, this.Error?.Exception);
    }

    public static ExecutionResult operator +(ExecutionResult left, ExecutionResult right) =>
        Execution.Combine(left, right);
}
