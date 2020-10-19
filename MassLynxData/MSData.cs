
namespace MSFileInfoScanner.MassLynxData
{
    class MSData
    {
        #region "Properties"

        public string UserSuppliedDataDirPath { get; set; }

        // The currently loaded data file path
        public string CurrentDataDirPath { get; set; }

        public MSHeaderInfo HeaderInfo { get; set; }

        public int FunctionCount { get; private set; }

        // 1-based array (to stay consistent with Micromass VB example conventions)
        public MSFunctionInfo[] FunctionInfo { get; private set; }

        #endregion

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
