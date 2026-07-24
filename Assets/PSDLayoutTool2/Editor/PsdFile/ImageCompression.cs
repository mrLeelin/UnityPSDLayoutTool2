namespace PhotoshopFile
{
    /// <summary>
    /// The possible Compression methods.
    /// </summary>
    public enum ImageCompression
    {
        /// <summary>
        /// No compression.
        /// </summary>
        Raw = 0,

        /// <summary>
        /// RLE compression.
        /// </summary>
        Rle = 1,

        /// <summary>
        /// ZIP compression without prediction.
        /// </summary>
        Zip = 2,

        /// <summary>
        /// ZIP compression with prediction.
        /// </summary>
        ZipPrediction = 3
    }
}
