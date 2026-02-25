using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
        [DefaultExecutionOrder(-102)]
        public class UnityMainThreadDispatcher : MonoBehaviour
        {
                private static readonly Queue<Action> executionQueue = new Queue<Action>();
                private static UnityMainThreadDispatcher instance;
                private static int mainThreadId;

                public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == mainThreadId;

		/// <summary>
		/// Singleton Instance accessor.
		/// </summary>
                public static UnityMainThreadDispatcher Instance()
                {
                        if (instance == null)
                        {
                                var dispatcherGameObject = new GameObject("UnityMainThreadDispatcher");
#if UNITY_EDITOR
                                if (!Application.isPlaying)
                                        dispatcherGameObject.hideFlags = HideFlags.HideAndDontSave;
#endif
                                instance = dispatcherGameObject.AddComponent<UnityMainThreadDispatcher>();
                                if (Application.isPlaying)
                                        DontDestroyOnLoad(dispatcherGameObject);
                        }
                        return instance;
                }

		private void Awake()
		{
                        if (instance == null)
                        {
                                instance = this;
                                mainThreadId = Thread.CurrentThread.ManagedThreadId;
                                if (Application.isPlaying)
                                        DontDestroyOnLoad(gameObject);
                        }
                        else if (instance != this)
                        {
                                Destroy(gameObject);
                        }
		}

		private void OnDestroy()
		{
			if (instance == this)
			{
				instance = null;
			}
		}

                private void Update()
                {
                        lock (executionQueue)
                        {
                                while (executionQueue.Count > 0)
                                {
                                        var action = executionQueue.Dequeue();
                                        try
                                        {
                                                action?.Invoke();
                                        }
                                        catch (Exception ex)
                                        {
                                                Debug.LogException(ex);
                                        }
                                }
                        }
                }

		/// <summary>
		/// Enqueues an action to be executed on the main thread.
		/// </summary>
		/// <param name="action">The action to execute.</param>
		public void Enqueue(Action action)
		{
			if (action == null) throw new ArgumentNullException(nameof(action));
			lock (executionQueue)
			{
				executionQueue.Enqueue(action);
			}
		}

		/// <summary>
		/// Enqueues a coroutine to be started on the main thread.
		/// </summary>
		/// <param name="coroutine">The coroutine to start.</param>
		public void Enqueue(IEnumerator coroutine)
		{
			if (coroutine == null) throw new ArgumentNullException(nameof(coroutine));
			Enqueue(() => StartCoroutine(coroutine));
		}

		/// <summary>
                /// Enqueues an action to be executed asynchronously on the main thread.
                /// </summary>
                /// <param name="action">The action to execute.</param>
                /// <returns>A task that completes when the action has been executed.</returns>
                public Task EnqueueAsync(Action action)
                {
                        if (action == null) throw new ArgumentNullException(nameof(action));
                        var tcs = new TaskCompletionSource<bool>();
                        Enqueue(() =>
                        {
                                try
                                {
                                        action();
                                        tcs.SetResult(true);
                                }
                                catch (Exception ex)
                                {
                                        tcs.SetException(ex);
                                }
                        });
                        return tcs.Task;
                }

                /// <summary>
                /// Enqueues a function to be executed asynchronously on the main thread and returns its result.
                /// </summary>
                /// <typeparam name="T">The return type of the function.</typeparam>
                /// <param name="func">The function to execute.</param>
                /// <returns>A task that completes with the result of the function.</returns>
                public Task<T> EnqueueAsync<T>(Func<T> func)
                {
                        if (func == null) throw new ArgumentNullException(nameof(func));
                        var tcs = new TaskCompletionSource<T>();
                        Enqueue(() =>
                        {
                                try
                                {
                                        T result = func();
                                        tcs.SetResult(result);
                                }
                                catch (Exception ex)
                                {
                                        tcs.SetException(ex);
                                }
                        });
                        return tcs.Task;
                }

                /// <summary>
                /// Enqueues an asynchronous function to be executed on the main thread and returns its result.
                /// </summary>
                /// <param name="func">The asynchronous function to execute.</param>
                public Task EnqueueAsync(Func<Task> func)
                {
                        if (func == null) throw new ArgumentNullException(nameof(func));
                        var tcs = new TaskCompletionSource<bool>();
                        Enqueue(async () =>
                        {
                                try
                                {
                                        await func().ConfigureAwait(false);
                                        tcs.SetResult(true);
                                }
                                catch (Exception ex)
                                {
                                        tcs.SetException(ex);
                                }
                        });
                        return tcs.Task;
                }

                /// <summary>
                /// Enqueues an asynchronous function with a return value to be executed on the main thread.
                /// </summary>
                /// <typeparam name="T">The return type of the function.</typeparam>
                /// <param name="func">The asynchronous function to execute.</param>
                public Task<T> EnqueueAsync<T>(Func<Task<T>> func)
                {
                        if (func == null) throw new ArgumentNullException(nameof(func));
                        var tcs = new TaskCompletionSource<T>();
                        Enqueue(async () =>
                        {
                                try
                                {
                                        T result = await func().ConfigureAwait(false);
                                        tcs.SetResult(result);
                                }
                                catch (Exception ex)
                                {
                                        tcs.SetException(ex);
                                }
                        });
                        return tcs.Task;
                }
	}
}
