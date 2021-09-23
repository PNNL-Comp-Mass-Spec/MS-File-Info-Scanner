
namespace MSFileInfoScanner.MassLynxData
{
    internal class MSData
    {
        public string UserSuppliedDataDirPath { get; set; }

        // The currently loaded data file path
        public string CurrentDataDirPath { get; set; }

        public MSHeaderInfo HeaderInfo { get; set; }

        public int FunctionCount { get; private set; }

        /// <summary>
        /// Function Info
        /// </summary>
        /// <remarks>
        /// This is a 1-based array (to stay consistent with MassLynx VB example conventions)
        /// </remarks>
        public MSFunctionInfo[] FunctionInfo { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public MSData()
        {
            InitializeFunctionInfo(0);
        }

        public void InitializeFunctionInfo(int functionCount)
        {
            FunctionCount = functionCount;

            // Note that the function array is 1-based
            FunctionInfo = new MSFunctionInfo[FunctionCount + 1];

            for (var functionNumber = 1; functionNumber <= FunctionCount; functionNumber++)
            {
                FunctionInfo[functionNumber] = new MSFunctionInfo(functionNumber);
            }
        }
    }
}
