using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Extensions.Unity.ImageLoader
{
    public partial class Future<T>
    {
        private bool isLoadingStarted = false;
        public UniTask StartLoading(bool ignoreImageNotFoundError = false)
        {
            if (isLoadingStarted)
            {
                if (LogLevel.IsActive(DebugLevel.Warning))
                    Debug.Log($"[ImageLoader] Future[id={Id}] Can't start loading, it is already started. Make a new Future instance to start another loading\n{Url}");
                return default;
            }

            isLoadingStarted = true;
            return InternalLoading(ignoreImageNotFoundError);
        }
        internal async UniTask InternalLoading(bool ignoreImageNotFoundError = false)
        {
            if (IsCancelled || Status == FutureStatus.Disposed || cleared)
                return;

            if (string.IsNullOrEmpty(Url))
            {
                ((IFutureInternal<T>)this).FailToLoad(new Exception($"[ImageLoader] Future[id={Id}] Empty url. Image could not be loaded!"));
                return;
            }

            if (UseMemoryCache && MemoryCacheContains(Url))
            {
                var cachedObj = LoadFromMemoryCache(Url);
                if (cachedObj != null)
                {
                    ((IFutureInternal<T>)this).SetLoaded(cachedObj, FutureLoadedFrom.MemoryCache);
                    return;
                }
            }

            if (!RegisterLoading(out var anotherLoadingFuture)) // LOADING ADDED
            {
                if (LogLevel.IsActive(DebugLevel.Log))
                    Debug.Log($"[ImageLoader] Future[id={Id}] Waiting while another task is loading\n{Url}");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                anotherLoadingFuture.PassEvents(this, passCancelled: false);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                await UniTask.WaitWhile(() => IsLoading(Url) && !IsCancelled);

                if (LogLevel.IsActive(DebugLevel.Log))
                {
                    Debug.Log(IsCancelled
                        ? $"[ImageLoader] Future[id={Id}] Cancelled\n{Url}"
                        : Status == FutureStatus.FailedToLoad
                            ? $"[ImageLoader] Future[id={Id}] Another task. Failed to load\n{Url}"
                            : $"[ImageLoader] Future[id={Id}] Another task. Complete waiting for another task to load\n{Url}");
                }

                if (IsCancelled || Status == FutureStatus.FailedToLoad)
                    return;

                InternalLoading(ignoreImageNotFoundError).Forget();
                return;
            }

            if (UseDiskCache && DiskCacheContains())
            {
                ((IFutureInternal<T>)this).Loading(FutureLoadingFrom.DiskCache);
                try
                {
                    var bytes = await LoadDiskAsync();
                    if (bytes != null && bytes.Length > 0)
                    {
                        await UniTask.SwitchToMainThread();
                        if (IsCancelled || Status == FutureStatus.FailedToLoad)
                        {
                            RemoveLoading(); // LOADING REMOVED
                            return;
                        }
                        var loadedObj = ParseBytes(bytes);
                        if (loadedObj != null)
                        {
                            if (UseMemoryCache)
                                SaveToMemoryCache(loadedObj, replace: true);

                            RemoveLoading(); // LOADING REMOVED
                            if (IsCancelled || Status == FutureStatus.FailedToLoad)
                                return;
                            ((IFutureInternal<T>)this).SetLoaded(loadedObj, FutureLoadedFrom.DiskCache);
                            return;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    RemoveLoading(); // LOADING REMOVED
                    Cancel();
                    return;
                }
                catch (Exception e)
                {
                    if (LogLevel.IsActive(DebugLevel.Exception))
                        Debug.LogException(e);
                }
            }

            ((IFutureInternal<T>)this).Loading(FutureLoadingFrom.Source);

            try
            {
                var result = await LoadFromSourceAsync(ignoreImageNotFoundError, cts.Token);

                if (IsCancelled || Status == FutureStatus.FailedToLoad)
                {
                    RemoveLoading(); // LOADING REMOVED
                    return;
                }

                if (UseDiskCache && result.RawBytes != null && result.RawBytes.Length > 0)
                    await SaveDiskAsync(result.RawBytes);

                if (IsCancelled || Status == FutureStatus.FailedToLoad)
                {
                    RemoveLoading(); // LOADING REMOVED
                    return;
                }

                if (LogLevel.IsActive(DebugLevel.Trace))
                    Debug.Log($"[ImageLoader] Future[id={Id}] Processing source response\n{Url}");

                if (UseMemoryCache)
                    SaveToMemoryCache(result.Value, replace: true);
                RemoveLoading(); // LOADING REMOVED

                ((IFutureInternal<T>)this).SetLoaded(result.Value, FutureLoadedFrom.Source);
            }
            catch (OperationCanceledException)
            {
                RemoveLoading(); // LOADING REMOVED
                Cancel();
            }
            catch (Exception e)
            {
                if (LogLevel.IsActive(DebugLevel.Exception) && !ignoreImageNotFoundError)
                    Debug.LogException(e);
                RemoveLoading(); // LOADING REMOVED
                ((IFutureInternal<T>)this).FailToLoad(e);
            }
        }

        /// <summary>
        /// Load the asset from its primary source and return the result.
        /// <para>
        /// The default implementation uses <see cref="UnityEngine.Networking.UnityWebRequest"/>.
        /// Override in subclasses to provide an alternative source strategy (e.g. Unity Addressables).
        /// </para>
        /// <para>
        /// Throw <see cref="OperationCanceledException"/> on cancellation, or any other
        /// <see cref="Exception"/> on failure. On success return a <see cref="LoadResult{T}"/>
        /// carrying the parsed value and optional raw bytes for disk-caching.
        /// </para>
        /// </summary>
        protected virtual async UniTask<LoadResult<T>> LoadFromSourceAsync(bool ignoreImageNotFoundError, CancellationToken ct)
        {
            if (IsCancelled || Status == FutureStatus.FailedToLoad)
                throw new OperationCanceledException(ct);

            if (LogLevel.IsActive(DebugLevel.Trace))
                Debug.Log($"[ImageLoader] Future[id={Id}] Creating UnityWebRequest for loading from Source\n{Url}");

            // Ensure we are on the main thread — SendWebRequest() must be called there.
            // SwitchToMainThread is a no-op when already on the main thread.
            await UniTask.SwitchToMainThread(ct);

            if (IsCancelled || Status == FutureStatus.FailedToLoad)
                throw new OperationCanceledException(ct);

            var asyncOperation = SetWebRequest(CreateWebRequest(Url)).SendWebRequest();

            await UniTask.WaitUntil(() => asyncOperation.isDone || IsCancelled);

            if (LogLevel.IsActive(DebugLevel.Trace))
                Debug.Log($"[ImageLoader] Future[id={Id}] Completed UnityWebRequest for loading from Source\n{Url}");

            if (IsCancelled)
                throw new OperationCanceledException(ct);

            if (WebRequest == null)
                throw new Exception($"[ImageLoader] Future[id={Id}] UnityWebRequest is null. URL={Url}");

#if UNITY_2020_1_OR_NEWER
            var isError = WebRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success;
#else
            var isError = WebRequest.isNetworkError || WebRequest.isHttpError;
#endif
            if (isError)
            {
#if UNITY_2020_1_OR_NEWER
                throw new Exception($"[ImageLoader] Future[id={Id}] {WebRequest.result} {WebRequest.error}. URL={Url}");
#else
                throw new Exception($"[ImageLoader] Future[id={Id}] {WebRequest.error}. URL={Url}");
#endif
            }

            if (LogLevel.IsActive(DebugLevel.Log))
                Debug.Log($"[ImageLoader] Future[id={Id}] Loaded from Source. Processing...\n{Url}");

            var rawBytes    = WebRequest.downloadHandler.data;
            var parsedValue = ParseWebRequest(WebRequest);
            return new LoadResult<T>(parsedValue, rawBytes);
        }

        protected virtual bool RegisterLoading(out Future<T> anotherLoadingFuture)
            => RegisterLoading(this, out anotherLoadingFuture);
        protected virtual void RemoveLoading() => RemoveLoading(Url);
    }
}

