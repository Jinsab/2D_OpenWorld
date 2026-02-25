#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
public static class AsyncOperationExtensions
{
public static TaskAwaiter GetAwaiter(this AsyncOperation operation)
{
if (operation == null)
throw new ArgumentNullException(nameof(operation));

if (operation.isDone)
return Task.CompletedTask.GetAwaiter();

        var tcs = new TaskCompletionSource<bool>();
void OnCompleted(AsyncOperation _)
{
operation.completed -= OnCompleted;
tcs.TrySetResult(true);
}
        operation.completed += OnCompleted;
        return ((Task)tcs.Task).GetAwaiter();
}
}
}
#endif

