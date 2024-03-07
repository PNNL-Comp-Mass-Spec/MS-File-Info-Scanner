namespace MSFileInfoScanner.DatasetStats
{
    /// <summary>
    /// Contains statistical data for an extracted ion
    /// </summary>
    public readonly struct ExtractedIonStats
    {
        /// <summary>
        /// Target m/z of the extracted ion
        /// </summary>
        public double Mz { get; }

        /// <summary>
        /// Maximum observed intensity of the extracted ion
        /// </summary>
        public double MaxIntensity { get; }

        /// <summary>
        /// Median observed intensity of the extracted ion
        /// </summary>
        public double MedianIntensity { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mz"></param>
        /// <param name="maxIntensity"></param>
        /// <param name="medianIntensity"></param>
        public ExtractedIonStats(double mz, double maxIntensity, double medianIntensity)
        {
            Mz = mz;
            MaxIntensity = maxIntensity;
            MedianIntensity = medianIntensity;
        }

        /// <summary>
        /// ToString override
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Mz:F2}: Max {MaxIntensity:F0}, Median {MedianIntensity:F0}";
        }
    }
}
