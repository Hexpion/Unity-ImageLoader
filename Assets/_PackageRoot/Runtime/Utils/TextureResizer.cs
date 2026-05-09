using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Extensions.Unity.ImageLoader
{
    /// <summary>
    /// GPU-accelerated texture downscaling utilities.
    /// <para>
    /// Use <see cref="Resize"/> to shrink a <see cref="Texture2D"/> while preserving its aspect
    /// ratio. Use <see cref="ResizeBytes"/> to decode raw PNG/JPG bytes, resize, and re-encode as
    /// PNG — useful for pre-generating low-resolution disk-cache entries.
    /// </para>
    /// </summary>
    public static class TextureResizer
    {
        /// <summary>
        /// Resize <paramref name="source"/> so it fits within <paramref name="maxWidth"/> ×
        /// <paramref name="maxHeight"/> while preserving the original aspect ratio.
        /// Uses a <see cref="RenderTexture"/> + <c>Graphics.Blit</c> pipeline for GPU-accelerated
        /// bilinear downscaling.
        /// <para><strong>Must be called from the main thread.</strong></para>
        /// </summary>
        /// <param name="source">The source texture to resize.</param>
        /// <param name="maxWidth">Maximum width of the output texture in pixels.</param>
        /// <param name="maxHeight">Maximum height of the output texture in pixels.</param>
        /// <returns>A new <see cref="Texture2D"/> at the computed target dimensions.</returns>
        public static Texture2D Resize(Texture2D source, int maxWidth, int maxHeight)
        {
            if (source == null)
                throw new System.ArgumentNullException(nameof(source));
            if (maxWidth  <= 0) throw new System.ArgumentOutOfRangeException(nameof(maxWidth));
            if (maxHeight <= 0) throw new System.ArgumentOutOfRangeException(nameof(maxHeight));

            // Compute target size while preserving aspect ratio.
            float aspect      = (float)source.width / source.height;
            int targetWidth   = maxWidth;
            int targetHeight  = Mathf.RoundToInt(maxWidth / aspect);
            if (targetHeight > maxHeight)
            {
                targetHeight = maxHeight;
                targetWidth  = Mathf.RoundToInt(maxHeight * aspect);
            }
            targetWidth  = Mathf.Max(1, targetWidth);
            targetHeight = Mathf.Max(1, targetHeight);

            var rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, rt);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var result = new Texture2D(targetWidth, targetHeight, TextureFormat.ARGB32, mipChain: false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        /// <summary>
        /// Decode <paramref name="sourceBytes"/> as a PNG or JPG image, resize the result using
        /// <see cref="Resize"/>, and return the output re-encoded as a PNG byte array.
        /// Returns <c>null</c> if the bytes cannot be decoded.
        /// <para><strong>Must be called from the main thread.</strong></para>
        /// </summary>
        /// <param name="sourceBytes">Raw PNG or JPG image bytes.</param>
        /// <param name="maxWidth">Maximum width of the output image in pixels.</param>
        /// <param name="maxHeight">Maximum height of the output image in pixels.</param>
        /// <param name="textureFormat">
        /// Texture format used when decoding <paramref name="sourceBytes"/> to a temporary texture.
        /// Defaults to <see cref="TextureFormat.ARGB32"/>.
        /// </param>
        /// <param name="mipChain">Whether to generate mipmaps for the temporary decode texture.</param>
        /// <returns>PNG-encoded bytes of the resized image, or <c>null</c> on failure.</returns>
        public static byte[] ResizeBytes(
            byte[] sourceBytes,
            int maxWidth,
            int maxHeight,
            TextureFormat textureFormat = TextureFormat.ARGB32,
            bool mipChain = false)
        {
            if (sourceBytes == null || sourceBytes.Length == 0) return null;

            var temp = new Texture2D(2, 2, textureFormat, mipChain);
            if (!temp.LoadImage(sourceBytes))
            {
                Object.DestroyImmediate(temp);
                return null;
            }

            var resized = Resize(temp, maxWidth, maxHeight);
            Object.DestroyImmediate(temp);

            var png = resized.EncodeToPNG();
            Object.DestroyImmediate(resized);

            return png;
        }

        /// <summary>
        /// Asynchronous variant of <see cref="Resize"/> that switches to the main thread for
        /// GPU work before performing the resize. Safe to await from background threads.
        /// </summary>
        /// <param name="source">The source texture to resize.</param>
        /// <param name="maxWidth">Maximum width of the output texture in pixels.</param>
        /// <param name="maxHeight">Maximum height of the output texture in pixels.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A new <see cref="Texture2D"/> at the computed target dimensions.</returns>
        public static async UniTask<Texture2D> ResizeAsync(
            Texture2D source,
            int maxWidth,
            int maxHeight,
            CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            return Resize(source, maxWidth, maxHeight);
        }
    }
}
