namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEditor;

    internal interface IPsdHierarchyWebMainThread
    {
        Task InvokeAsync(Func<Task> action);
        Task<TResult> InvokeAsync<TResult>(Func<TResult> action);
    }

    internal sealed class PsdHierarchyWebMainThread : IPsdHierarchyWebMainThread
    {
        private readonly int mainThreadId;

        public PsdHierarchyWebMainThread()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public Task InvokeAsync(Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (Thread.CurrentThread.ManagedThreadId == mainThreadId) return action();

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EditorApplication.delayCall += async () =>
            {
                try
                {
                    await action();
                    completion.TrySetResult(true);
                }
                catch (OperationCanceledException) { completion.TrySetCanceled(); }
                catch (Exception exception) { completion.TrySetException(exception); }
            };
            return completion.Task;
        }

        public Task<TResult> InvokeAsync<TResult>(Func<TResult> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (Thread.CurrentThread.ManagedThreadId == mainThreadId) return Task.FromResult(action());

            var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            EditorApplication.delayCall += () =>
            {
                try { completion.TrySetResult(action()); }
                catch (Exception exception) { completion.TrySetException(exception); }
            };
            return completion.Task;
        }
    }
}
