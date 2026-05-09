using System.Collections.Concurrent;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Extensions.Unity.ImageLoader
{
    /// <summary>
    /// A <see cref="FutureSprite"/> that loads its asset via Unity Addressables instead of a
    /// <see cref="UnityEngine.Networking.UnityWebRequest"/>.
    /// <para>
    /// Pass the Addressables <em>address</em>, <em>label</em>, or <em>AssetReference key</em> as
    /// the <c>addressableKey</c> constructor parameter.
    /// </para>
    /// <para>
    /// Memory caching and disk caching are disabled by default because Addressables manages its
    /// own internal download and bundle cache.
    /// </para>
    /// </summary>
    public class FutureAddressableSprite : FutureSprite
    {
        // Maps each loaded Sprite instance → its Addressable handle for later release.
        private static readonly ConcurrentDictionary<Sprite, AsyncOperationHandle<Sprite>>
            s_handles = new ConcurrentDictionary<Sprite, AsyncOperationHandle<Sprite>>();

        public FutureAddressableSprite(
            string addressableKey,
            float pixelDensity = 100,
            CancellationToken cancellationToken = default)
            : this(addressableKey, new Vector2(.5f, .5f), pixelDensity,
                   TextureFormat.ARGB32, mipChain: true, cancellationToken)
        { }

        public FutureAddressableSprite(
            string addressableKey,
            Vector2 pivot,
            float pixelDensity = 100,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = true,
            CancellationToken cancellationToken = default)
            : base(addressableKey, pivot, pixelDensity, textureFormat, mipChain, cancellationToken)
        {
            SetUseDiskCache(false);
            SetUseMemoryCache(false);
        }

        /// <inheritdoc/>
        protected override async UniTask<LoadResult<Sprite>> LoadFromSourceAsync(
            bool ignoreImageNotFoundError, CancellationToken ct)
        {
            if (IsCancelled || Status == FutureStatus.FailedToLoad)
                throw new System.OperationCanceledException(ct);

            if (LogLevel.IsActive(DebugLevel.Trace))
                Debug.Log($"[ImageLoader] Future[id={Id}] Addressables.LoadAssetAsync<Sprite>\n{Url}");

            var handle = Addressables.LoadAssetAsync<Sprite>(Url);

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
                    ?? new System.Exception($"[ImageLoader] Addressables failed to load Sprite. Key={Url}");
                Addressables.Release(handle);
                throw ex;
            }

            var sprite = handle.Result;

            // Associate handle with the sprite instance for later release.
            s_handles[sprite] = handle;

            if (LogLevel.IsActive(DebugLevel.Log))
                Debug.Log($"[ImageLoader] Future[id={Id}] Loaded Sprite via Addressables\n{Url}");

            return new LoadResult<Sprite>(sprite, rawBytes: null);
        }

        /// <summary>
        /// Releases the Addressable handle associated with <paramref name="obj"/> instead of
        /// calling <c>Object.DestroyImmediate</c>. Addressables manages the asset lifetime.
        /// </summary>
        protected override void ReleaseMemory(Sprite obj, DebugLevel logLevel = DebugLevel.Log)
        {
            if (obj == null) return;

            if (s_handles.TryRemove(obj, out var handle))
            {
                if (logLevel.IsActive(DebugLevel.Trace))
                    Debug.Log($"[ImageLoader] Releasing Addressable handle for Sprite\n{Url}");

                Addressables.Release(handle);
                // Do NOT call Object.DestroyImmediate — Addressables owns the asset lifetime.
            }
        }
    }
}
