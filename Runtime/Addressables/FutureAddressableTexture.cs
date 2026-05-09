using System.Collections.Concurrent;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Extensions.Unity.ImageLoader
{
    /// <summary>
    /// A <see cref="FutureTexture"/> that loads its asset via Unity Addressables instead of a
    /// <see cref="UnityEngine.Networking.UnityWebRequest"/>.
    /// <para>
    /// Pass the Addressables <em>address</em>, <em>label</em>, or <em>AssetReference key</em> as
    /// the <c>addressableKey</c> constructor parameter (the same value used as the cache key).
    /// </para>
    /// <para>
    /// Memory caching and disk caching are disabled by default because Addressables manages its
    /// own internal download and bundle cache. Enable them explicitly via
    /// <see cref="IFuture{T}.SetUseMemoryCache"/> / <see cref="IFuture{T}.SetUseDiskCache"/> if
    /// you need the additional layers, but note that you are then responsible for avoiding
    /// <c>Object.DestroyImmediate</c> being called on Addressables-owned assets when the cache is
    /// cleared globally.
    /// </para>
    /// </summary>
    public class FutureAddressableTexture : FutureTexture
    {
        // Maps each loaded Texture2D instance → its Addressable handle so we can release it when
        // ReleaseMemory is called for that specific texture object.
        private static readonly ConcurrentDictionary<Texture2D, AsyncOperationHandle<Texture2D>>
            s_handles = new ConcurrentDictionary<Texture2D, AsyncOperationHandle<Texture2D>>();

        public FutureAddressableTexture(
            string addressableKey,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = true,
            CancellationToken cancellationToken = default)
            : base(addressableKey, textureFormat, mipChain, cancellationToken)
        {
            // Addressables manages its own caching; skip ImageLoader's disk and memory layers.
            SetUseDiskCache(false);
            SetUseMemoryCache(false);
        }

        /// <inheritdoc/>
        protected override async UniTask<LoadResult<Texture2D>> LoadFromSourceAsync(
            bool ignoreImageNotFoundError, CancellationToken ct)
        {
            if (IsCancelled || Status == FutureStatus.FailedToLoad)
                throw new System.OperationCanceledException(ct);

            if (LogLevel.IsActive(DebugLevel.Trace))
                Debug.Log($"[ImageLoader] Future[id={Id}] Addressables.LoadAssetAsync<Texture2D>\n{Url}");

            var handle = Addressables.LoadAssetAsync<Texture2D>(Url);

            try
            {
                await UniTask.WaitUntil(() => handle.IsDone || ct.IsCancellationRequested);
            }
            catch
            {
                Addressables.Release(handle);
                throw;
            }

            if (ct.IsCancellationRequested)
            {
                Addressables.Release(handle);
                throw new System.OperationCanceledException(ct);
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                var ex = handle.OperationException
                    ?? new System.Exception($"[ImageLoader] Addressables failed to load Texture2D. Key={Url}");
                Addressables.Release(handle);
                throw ex;
            }

            var texture = handle.Result;

            // Associate handle with the texture instance for later release.
            // Guard: if the same Texture2D reference somehow appears again, only release the old
            // handle when it is genuinely different from the new one (avoids a double-release).
            s_handles.AddOrUpdate(
                texture,
                addValueFactory:    _           => handle,
                updateValueFactory: (_, oldHandle) =>
                {
                    if (!oldHandle.Equals(handle))
                        Addressables.Release(oldHandle);
                    return handle;
                });


            if (LogLevel.IsActive(DebugLevel.Log))
                Debug.Log($"[ImageLoader] Future[id={Id}] Loaded Texture2D via Addressables\n{Url}");

            // RawBytes is null: Addressables provides a live asset, not raw download bytes.
            return new LoadResult<Texture2D>(texture, rawBytes: null);
        }

        /// <summary>
        /// Releases the Addressable handle associated with <paramref name="obj"/> instead of
        /// calling <c>Object.DestroyImmediate</c>. Addressables manages the asset lifetime.
        /// </summary>
        protected override void ReleaseMemory(Texture2D obj, DebugLevel logLevel = DebugLevel.Log)
        {
            if (obj == null) return;

            if (s_handles.TryRemove(obj, out var handle))
            {
                if (logLevel.IsActive(DebugLevel.Trace))
                    Debug.Log($"[ImageLoader] Releasing Addressable handle for Texture2D\n{Url}");

                Addressables.Release(handle);
                // Do NOT call Object.DestroyImmediate — Addressables owns the asset lifetime.
            }
        }

        /// <summary>
        /// Release the Addressable handle for a given texture instance if one is registered.
        /// Called by <see cref="FutureAddressableSprite"/> when it needs to release the underlying
        /// texture of a sprite that was loaded via Addressables.
        /// </summary>
        internal static bool TryReleaseHandle(Texture2D texture, DebugLevel logLevel)
        {
            if (texture == null) return false;

            if (s_handles.TryRemove(texture, out var handle))
            {
                if (logLevel.IsActive(DebugLevel.Trace))
                    Debug.Log($"[ImageLoader] Releasing Addressable handle for Texture2D (via TryReleaseHandle)");

                Addressables.Release(handle);
                return true;
            }

            return false;
        }
    }
}
