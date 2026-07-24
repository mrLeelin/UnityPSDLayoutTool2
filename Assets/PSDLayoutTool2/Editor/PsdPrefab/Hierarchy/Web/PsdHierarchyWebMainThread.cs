namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IPsdHierarchyWebMainThread
    {
        Task InvokeAsync(Func<Task> action);
        Task<TResult> InvokeAsync<TResult>(Func<TResult> action);
    }

    internal sealed class PsdHierarchyWebMainThread : IPsdHierarchyWebMainThread
    {
        private readonly int mainThreadId;
        private readonly SynchronizationContext synchronizationContext;

        public PsdHierarchyWebMainThread()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            synchronizationContext = SynchronizationContext.Current ??
                throw new InvalidOperationException("Unity main-thread synchronization context is unavailable.");
        }

        public Task InvokeAsync(Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (Thread.CurrentThread.ManagedThreadId == mainThreadId) return action();

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            synchronizationContext.Post(async _ =>
            {
                try
                {
                    await action();
                    completion.TrySetResult(true);
                }
                catch (OperationCanceledException) { completion.TrySetCanceled(); }
                catch (Exception exception) { completion.TrySetException(exception); }
            }, null);
            return completion.Task;
        }

        public Task<TResult> InvokeAsync<TResult>(Func<TResult> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (Thread.CurrentThread.ManagedThreadId == mainThreadId) return Task.FromResult(action());

            var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            synchronizationContext.Post(_ =>
            {
                try { completion.TrySetResult(action()); }
                catch (Exception exception) { completion.TrySetException(exception); }
            }, null);
            return completion.Task;
        }
    }
}
