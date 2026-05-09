using System.Threading;
using UnityEngine;

namespace Extensions.Unity.ImageLoader
{
    public static partial class ImageLoader
    {
        /// <summary>
        /// Load a low-resolution texture first (shown immediately as a placeholder), then load
        /// and replace it with the full-resolution version once it is available.
        /// <para>
        /// The low-resolution future is started concurrently with the full-resolution one. When
        /// the low-res image loads it is injected as a <see cref="PlaceholderTrigger.LoadingFromSource"/>
        /// placeholder on the full-res future so every registered <c>Consume</c> consumer receives
        /// the low-res texture immediately. Once the full-resolution image loads it replaces the
        /// low-res content and the low-res future is disposed.
        /// </para>
        /// </summary>
        /// <param name="lowResUrl">URL / local path of the low-resolution texture to show first.</param>
        /// <param name="fullResUrl">URL / local path of the full-resolution texture.</param>
        /// <returns>
        /// The full-resolution <see cref="FutureTexture"/>. Attach <c>Consume</c> and other
        /// callbacks to this future as normal; the low-res placeholder is handled automatically.
        /// </returns>
        public static FutureTexture LoadTextureProgressive(
            string lowResUrl,
            string fullResUrl,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = true,
            bool ignoreImageNotFoundError = false,
            CancellationToken cancellationToken = default)
        {
            var fullResFuture = LoadTexture(fullResUrl, textureFormat, mipChain, ignoreImageNotFoundError, cancellationToken);
            fullResFuture.WithLowResPlaceholder(lowResUrl, textureFormat, mipChain, cancellationToken);
            return fullResFuture;
        }

        /// <summary>
        /// Load a low-resolution sprite first (shown immediately as a placeholder), then load and
        /// replace it with the full-resolution version once it is available.
        /// </summary>
        /// <param name="lowResUrl">URL / local path of the low-resolution sprite to show first.</param>
        /// <param name="fullResUrl">URL / local path of the full-resolution sprite.</param>
        public static FutureSprite LoadSpriteProgressive(
            string lowResUrl,
            string fullResUrl,
            float pixelDensity = 100,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = true,
            bool ignoreImageNotFoundError = false,
            CancellationToken cancellationToken = default)
            => LoadSpriteProgressive(lowResUrl, fullResUrl, new Vector2(0.5f, 0.5f),
                                     pixelDensity, textureFormat, mipChain,
                                     ignoreImageNotFoundError, cancellationToken);

        /// <summary>
        /// Load a low-resolution sprite first (shown immediately as a placeholder), then load and
        /// replace it with the full-resolution version once it is available.
        /// </summary>
        /// <param name="lowResUrl">URL / local path of the low-resolution sprite to show first.</param>
        /// <param name="fullResUrl">URL / local path of the full-resolution sprite.</param>
        /// <param name="pivot">Pivot point used when constructing the Sprite.</param>
        public static FutureSprite LoadSpriteProgressive(
            string lowResUrl,
            string fullResUrl,
            Vector2 pivot,
            float pixelDensity = 100,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = true,
            bool ignoreImageNotFoundError = false,
            CancellationToken cancellationToken = default)
        {
            var fullResFuture = LoadSprite(fullResUrl, pivot, pixelDensity, textureFormat,
                                           mipChain, ignoreImageNotFoundError, cancellationToken);
            fullResFuture.WithLowResPlaceholder(lowResUrl, pivot, pixelDensity, textureFormat,
                                                mipChain, cancellationToken);
            return fullResFuture;
        }
    }
}
