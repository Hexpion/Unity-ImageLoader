using System.Threading;
using UnityEngine;

namespace Extensions.Unity.ImageLoader
{
    /// <summary>
    /// Entry points for loading assets via Unity Addressables.
    /// This class lives in the optional <c>Extensions.Unity.ImageLoader.Addressables</c> assembly,
    /// which is only compiled when the <c>com.unity.addressables</c> package is present in the project.
    /// </summary>
    public static class ImageLoaderAddressables
    {
        /// <summary>
        /// Load a <see cref="UnityEngine.Texture2D"/> from Addressables by its address/key.
        /// <para>
        /// The <paramref name="addressableKey"/> is used as the Addressables loading key and as
        /// the internal cache key, so it must uniquely identify the asset within the project.
        /// </para>
        /// <para>
        /// Memory caching and disk caching are disabled by default; Addressables manages its own
        /// internal cache. Enable them via the returned future's <c>SetUseMemoryCache</c> /
        /// <c>SetUseDiskCache</c> methods if needed.
        /// </para>
        /// </summary>
        /// <param name="addressableKey">Addressables address, label, or AssetReference key.</param>
        public static FutureAddressableTexture LoadTexture(
            string addressableKey,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = true,
            bool ignoreImageNotFoundError = false,
            CancellationToken cancellationToken = default)
        {
            var future = new FutureAddressableTexture(addressableKey, textureFormat, mipChain, cancellationToken);
            future.StartLoading(ignoreImageNotFoundError);
            return future;
        }

        /// <summary>
        /// Load a <see cref="UnityEngine.Sprite"/> from Addressables by its address/key.
        /// </summary>
        /// <param name="addressableKey">Addressables address, label, or AssetReference key.</param>
        public static FutureAddressableSprite LoadSprite(
            string addressableKey,
            float pixelDensity = 100,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = true,
            bool ignoreImageNotFoundError = false,
            CancellationToken cancellationToken = default)
            => LoadSprite(addressableKey, new Vector2(0.5f, 0.5f), pixelDensity,
                          textureFormat, mipChain, ignoreImageNotFoundError, cancellationToken);

        /// <summary>
        /// Load a <see cref="UnityEngine.Sprite"/> from Addressables by its address/key.
        /// </summary>
        /// <param name="addressableKey">Addressables address, label, or AssetReference key.</param>
        /// <param name="pivot">Pivot point used when constructing the Sprite.</param>
        public static FutureAddressableSprite LoadSprite(
            string addressableKey,
            Vector2 pivot,
            float pixelDensity = 100,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = true,
            bool ignoreImageNotFoundError = false,
            CancellationToken cancellationToken = default)
        {
            var future = new FutureAddressableSprite(addressableKey, pivot, pixelDensity,
                                                     textureFormat, mipChain, cancellationToken);
            future.StartLoading(ignoreImageNotFoundError);
            return future;
        }
    }
}
