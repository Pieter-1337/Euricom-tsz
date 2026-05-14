namespace Tsz.Infrastructure.Abstractions;

public interface ICommandHandler<in TCommand, TResult> : IRequestHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>;
