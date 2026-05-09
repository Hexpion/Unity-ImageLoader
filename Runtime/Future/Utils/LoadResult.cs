namespace Extensions.Unity.ImageLoader
{
    /// <summary>
    /// Carries the result of loading an asset from its primary source.
    /// <para><see cref="Value"/> is the parsed asset ready for use.</para>
    /// <para><see cref="RawBytes"/> contains the raw download bytes for disk-caching.
    /// It may be <c>null</c> when the source does not provide raw bytes (e.g. Addressables).</para>
    /// </summary>
    public readonly struct LoadResult<T>
    {
        /// <summary>The parsed asset value.</summary>
        public readonly T Value;

        /// <summary>
        /// Raw bytes of the downloaded asset, used for disk-caching.
        /// <c>null</c> when not applicable (e.g. Addressables-based loading).
        /// </summary>
        public readonly byte[] RawBytes;

        public LoadResult(T value, byte[] rawBytes = null)
        {
            Value    = value;
            RawBytes = rawBytes;
        }
    }
}
