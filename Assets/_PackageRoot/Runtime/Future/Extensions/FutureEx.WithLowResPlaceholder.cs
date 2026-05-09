using System.Threading;
using UnityEngine;

namespace Extensions.Unity.ImageLoader
{
    public static partial class FutureEx
    {
        /// <summary>
        /// Load a low-resolution texture from <paramref name="lowResUrl"/> and use it as an
        /// instant placeholder while the full-resolution <paramref name="future"/> is still loading
        /// from its source.
        /// <para>
        /// When the low-res image finishes loading it is injected as a
        /// <see cref="PlaceholderTrigger.LoadingFromSource"/> placeholder on
        /// <paramref name="future"/>. All consumers registered on the future immediately receive
        /// the low-res texture, and any consumers that subscribe later (while the future is still
        /// loading) will also get the low-res texture right away. When the full-res image loads it
        /// replaces the low-res content normally.
        /// </para>
        /// <para>
        /// The low-res future is disposed automatically once the full-res future completes
        /// (succeeds, fails, or is cancelled).
        /// </para>
        /// <para>
        /// This extension works with <em>any</em> <see cref="IFuture{T}"/> source, including
        /// <see cref="FutureAddressableTexture"/> — allowing patterns such as:
        /// <code>
        /// ImageLoaderAddressables.LoadTexture("enemies/boss_hd")
        ///     .WithLowResPlaceholder("enemies/boss_lowres")
        ///     .Consume(rawImage);
        /// </code>
        /// </para>
        /// </summary>
        /// <param name="lowResUrl">URL / local path / Addressables key of the low-resolution image.</param>
        /// <returns>The original <paramref name="future"/> for fluent chaining.</returns>
        public static IFuture<Texture2D> WithLowResPlaceholder(
            this IFuture<Texture2D> future,
            string lowResUrl,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = true,
            CancellationToken cancellationToken = default)
        {
            if (future.IsCompleted || future.IsCancelled || string.IsNullOrEmpty(lowResUrl))
                return future;

            // Start loading the low-res version in parallel; errors are silently ignored.
            var lowResFuture = ImageLoader.LoadTexture(
                lowResUrl, textureFormat, mipChain,
                ignoreImageNotFoundError: true, cancellationToken);

            // When the low-res arrives, push it as a placeholder on the full-res future.
            lowResFuture.Loaded(lowResTexture =>
            {
                if (!future.IsCompleted && !future.IsCancelled)
                    future.SetPlaceholder(lowResTexture, PlaceholderTrigger.LoadingFromSource);
            });

            // Dispose the low-res future once the full-res future reaches any terminal state.
            future.Completed(_ => lowResFuture.Dispose());

            return future;
        }

        /// <summary>
        /// Load a low-resolution sprite from <paramref name="lowResUrl"/> and use it as an instant
        /// placeholder while the full-resolution <paramref name="future"/> is still loading.
        /// </summary>
        /// <param name="lowResUrl">URL / local path of the low-resolution sprite.</param>
        /// <returns>The original <paramref name="future"/> for fluent chaining.</returns>
        public static IFuture<Sprite> WithLowResPlaceholder(
            this IFuture<Sprite> future,
            string lowResUrl,
            float pixelDensity = 100,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = true,
            CancellationToken cancellationToken = default)
            => future.WithLowResPlaceholder(lowResUrl, new Vector2(0.5f, 0.5f),
                                            pixelDensity, textureFormat, mipChain, cancellationToken);

        /// <summary>
        /// Load a low-resolution sprite from <paramref name="lowResUrl"/> and use it as an instant
        /// placeholder while the full-resolution <paramref name="future"/> is still loading.
        /// </summary>
        /// <param name="lowResUrl">URL / local path of the low-resolution sprite.</param>
        /// <param name="pivot">Pivot point used when loading the low-res sprite.</param>
        /// <returns>The original <paramref name="future"/> for fluent chaining.</returns>
        public static IFuture<Sprite> WithLowResPlaceholder(
            this IFuture<Sprite> future,
            string lowResUrl,
            Vector2 pivot,
            float pixelDensity = 100,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = true,
            CancellationToken cancellationToken = default)
        {
            if (future.IsCompleted || future.IsCancelled || string.IsNullOrEmpty(lowResUrl))
                return future;

            var lowResFuture = ImageLoader.LoadSprite(
                lowResUrl, pivot, pixelDensity, textureFormat, mipChain,
                ignoreImageNotFoundError: true, cancellationToken);

            lowResFuture.Loaded(lowResSprite =>
            {
                if (!future.IsCompleted && !future.IsCancelled)
                    future.SetPlaceholder(lowResSprite, PlaceholderTrigger.LoadingFromSource);
            });

            future.Completed(_ => lowResFuture.Dispose());

            return future;
        }
    }
}
