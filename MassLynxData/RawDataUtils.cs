using System;

namespace MSFileInfoScanner.MassLynxData
{
    internal class RawDataUtils
    {
        // Function Info Masks
        private short maskFunctionType;
        private short maskIonMode;
        private short maskAcquisitionDataType;
        private short maskCollisionEnergy;

        private int maskSegmentChannelCount;

        // Scan Info Masks
        private int maskSpectralPeak;
        private int maskSegment;
        private int maskUseFollowingContinuum;
        private int maskContinuumDataOverride;
        private int maskScanContainsMolecularMasses;

        private int maskScanContainsCalibratedMasses;

        // Packed mass and packed intensity masks
        private int maskBPIntensityScale;

        private int maskBPMassExponent;
        private int maskBPCompressedDataIntensityScale;

        private int maskBPCompressedDataIntensity;
        private int maskBPStandardDataIntensityScale;
        private int maskBPStandardDataIntensity;

        private int maskBPStandardDataMass;

        private int maskBPUncalibratedDataChannelNumber;

        /// <summary>
        /// Constructor
        /// </summary>
        public RawDataUtils()
        {
            CreateNativeDataMasks();
        }

        /// <summary>
        /// Create a mask
        /// </summary>
        /// <param name="startBit"></param>
        /// <param name="endBit"></param>
        /// <remarks>Returns a long value to allow for unsigned Int32 masks</remarks>
        private long CreateMask(byte startBit, byte endBit)
        {
            long thisMask;

            if (startBit == 0)
            {
                thisMask = (long)(Math.Pow(2, endBit + 1) - 1);
            }
            else
            {
                thisMask = 0;
                for (var bitIndex = startBit; bitIndex <= endBit; bitIndex++)
                {
                    thisMask += (long)Math.Pow(2, bitIndex);
                }
            }

            return thisMask;
        }

        private void CreateNativeDataMasks()
        {
            // Create the bit masks for the PackedFunctionInfo
            maskFunctionType = (short)CreateMask(0, 4);
            maskIonMode = (short)CreateMask(5, 9);
            maskAcquisitionDataType = (short)CreateMask(10, 13);

            // Create the bit masks for the Packed MS/MS Info
            maskCollisionEnergy = (short)CreateMask(0, 7);
            maskSegmentChannelCount = (int)CreateMask(8, 15);

            // Create the bit masks for the packed scan info
            maskSpectralPeak = (int)CreateMask(0, 21);
            maskSegment = (int)CreateMask(22, 26);
            maskUseFollowingContinuum = (int)CreateMask(27, 27);
            maskContinuumDataOverride = (int)CreateMask(28, 28);
            maskScanContainsMolecularMasses = (int)CreateMask(29, 29);
            maskScanContainsCalibratedMasses = (int)CreateMask(30, 30);

            // Create the masks for the packed base peak info
            maskBPIntensityScale = (int)CreateMask(0, 3);

            // Also applies to High Intensity Calibrated data and High Accuracy Calibrated Data
            maskBPMassExponent = (int)CreateMask(4, 8);

            maskBPCompressedDataIntensityScale = (int)CreateMask(0, 2);
            maskBPCompressedDataIntensity = (int)CreateMask(3, 10);

            maskBPStandardDataIntensityScale = (int)CreateMask(0, 2);

            // Also applies to Uncalibrated data
            maskBPStandardDataIntensity = (int)CreateMask(3, 15);

            // Also applies to Uncalibrated data
            maskBPStandardDataMass = (int)CreateMask(0, 23);

            maskBPUncalibratedDataChannelNumber = (int)CreateMask(0, 27);
        }

        private int ExtractFromBitsInt32(int packedValue, byte startBit, byte endBit)
        {
            int unpackedValue;

            if (endBit < 31)
            {
                if (startBit == 0)
                {
                    unpackedValue = (int)(packedValue & CreateMask(0, endBit));
                }
                else
                {
                    unpackedValue = (int)((long)(packedValue / Math.Pow(2, startBit)) & CreateMask(0, (byte)(endBit - startBit)));
                }
            }
            else
            {
                unpackedValue = (int)(NumberConversion.Int32ToUnsigned(packedValue) / Math.Pow(2, startBit));
            }

            return unpackedValue;
        }

        public short GetFunctionType(short packedFunctionInfo)
        {
            return (short)(packedFunctionInfo & maskFunctionType);
        }

        public short GetIonMode(short packedFunctionInfo)
        {
            return (short)((short)(packedFunctionInfo & maskIonMode) / 32f);     // 32 = 2^5
        }

        public short GetAcquisitionDataType(short packedFunctionInfo)
        {
            return (short)((short)(packedFunctionInfo & maskAcquisitionDataType) / 1024f);    // 1024 = 2^10
        }

        public short GetMsMsCollisionEnergy(short packedMsMsInfo)
        {
            return (short)(packedMsMsInfo & maskCollisionEnergy);
        }

        public short GetMSMSSegmentOrChannelCount(short packedMsMsInfo)
        {
            return (short)(NumberConversion.Int32ToUnsigned(packedMsMsInfo) / 256f);      // 256 = 2^8
        }

        public int GetNumSpectraPeaks(int packedScanInfo)
        {
            return packedScanInfo & maskSpectralPeak;
        }

        public short GetSegmentNumber(int packedScanInfo)
        {
            return (short)((short)(packedScanInfo & maskSegment) / 4194304);
        }

        public bool GetUseFollowingContinuum(int packedScanInfo)
        {
            return NumberConversion.ValueToBool(packedScanInfo & maskUseFollowingContinuum);
        }

        public bool GetContinuumDataOverride(int packedScanInfo)
        {
            return NumberConversion.ValueToBool(packedScanInfo & maskContinuumDataOverride);
        }

        public bool GetContainsMolecularMasses(int packedScanInfo)
        {
            return NumberConversion.ValueToBool(packedScanInfo & maskScanContainsMolecularMasses);
        }

        public bool GetContainsCalibratedMasses(int packedScanInfo)
        {
            return NumberConversion.ValueToBool(packedScanInfo & maskScanContainsCalibratedMasses);
        }

        /// <summary>
        /// Extracts packed intensity data
        /// </summary>
        /// <param name="packedBasePeakIntensity"></param>
        /// <param name="packedBasePeakInfo"></param>
        /// <param name="acquisitionDataType"></param>
        public float UnpackIntensity(short packedBasePeakIntensity, int packedBasePeakInfo, short acquisitionDataType)
        {
            // See note for Acquisition Data Types 9 to 12 below

            switch (acquisitionDataType)
            {
                case 9:
                case 10:
                case 11:
                case 12:
                    // Includes type 9, 11, and 12; type 10 is officially unused
                    //  (9=High accuracy calibrated data, 11=Enhanced uncalibrated data, and 12=Enhanced calibrated data)
                    // Note: Only use this function to unpack intensities for data in the .IDX file, not for data in the .DAT file
                    //       See the NativeIOGetSpectrum function for the method of unpacking intensities in .DAT files

                    //Debug.Assert unpackedIntensity = PackedBasePeakIntensity * 4 ^ ExtractFromBitsInt32(PackedBasePeakInfo, 0, 3)
                    return UnpackBasePeakIntensity(packedBasePeakIntensity, packedBasePeakInfo);

                case 0:
                    // Compressed data
                    //Debug.Assert unpackedIntensity = ExtractFromBitsInt32(PackedBasePeakInfo, 3, 10) * 4 ^ ExtractFromBitsInt32(PackedBasePeakInfo, 0, 2)
                    return UnpackBasePeakIntensityCompressed(packedBasePeakInfo);

                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                    // Standard data and Uncalibrated data
                    //Debug.Assert unpackedIntensity = ExtractFromBitsInt32(CInt(PackedBasePeakIntensity), 3, 15) * 4 ^ ExtractFromBitsInt32(CInt(PackedBasePeakIntensity), 0, 2)
                    return UnpackBasePeakIntensityStandard(packedBasePeakIntensity);

                case 8:
                    // High intensity calibrated data
                    return UnpackBasePeakIntensity(packedBasePeakIntensity, packedBasePeakInfo);

                default:
                    return 0;
            }
        }

        private float UnpackBasePeakIntensity(short packedBasePeakIntensity, int packedBasePeakInfo)
        {
            return (float)(packedBasePeakIntensity * Math.Pow(4, packedBasePeakInfo & maskBPIntensityScale));
        }

        private float UnpackBasePeakIntensityCompressed(int packedBasePeakInfo)
        {
            return (float)((short)(packedBasePeakInfo & maskBPCompressedDataIntensity) / 8f * Math.Pow(4, packedBasePeakInfo & maskBPCompressedDataIntensityScale));
        }

        private float UnpackBasePeakIntensityStandard(short packedBasePeakIntensity)
        {
            return (float)((short)(packedBasePeakIntensity & maskBPStandardDataIntensity) / 8f * Math.Pow(4, packedBasePeakIntensity & maskBPStandardDataIntensityScale));
        }

        /// <summary>
        /// Extracts packed mass data
        /// </summary>
        /// <param name="packedBasePeakInfo"></param>
        /// <param name="acquisitionDataType"></param>
        /// <param name="processingFunctionIndexFile"></param>
        public double UnpackMass(int packedBasePeakInfo, short acquisitionDataType, bool processingFunctionIndexFile)
        {
            // See note for Acquisition Data Types 9 to 12 below

            switch (acquisitionDataType)
            {
                case 9:
                case 10:
                case 11:
                case 12:
                    // Includes type 9, 11, and 12; type 10 is officially unused
                    //  (9=High accuracy calibrated data, 11=Enhanced uncalibrated data, and 12=Enhanced calibrated data)
                    // Note: Only use this function to unpack masses for data in the .IDX file, not for data in the .DAT file
                    //       See the NativeIOGetSpectrum function for the method of unpacking masses in .DAT files

                    // Compute the MassMantissa value by converting the Packed value to an Unsigned Int32 (and storing in a long),
                    //  then right shifting 9 bits by dividing by 2^9
                    // It would be more straightforward to use packedBasePeakInfo And CreateMask(9, 31) but VB won't let us
                    //  And a Currency value with a Long; this gives an OverFlow error
                    var massMantissa = (int)(NumberConversion.Int32ToUnsigned(packedBasePeakInfo) / 512f);      // 512 = 2^9

                    // Compute the MassExponent value by multiplying the Packed value by the appropriate BitMask, then right shifting 4 bits by dividing by 2^4
                    var massExponent = (short)(packedBasePeakInfo & maskBPMassExponent) / 16f;                  // 16 = 2^4

                    if (processingFunctionIndexFile)
                    {
                        // When computing the BasePeakMass based on data in the _func001.idx
                        //  file, must bump up the Mass Exponent by 8 in order to multiply the
                        //  Mass Mantissa by an additional value of 256 to get the correct value
                        if (massExponent < 6)
                        {
                            if (acquisitionDataType == 9)
                            {
                                // This only seems to be necessary for files with Acquisition data type 9
                                // The following Assertion is here to test for that
                                massExponent += 8;
                            }
                        }
                    }

                    // Note that we divide by 2^23 to convert the mass mantissa to fractional form
                    return massMantissa / 8388608f * Math.Pow(2, massExponent);      // 8388608 = 2^23

                case 0:
                    // Compressed data
                    // Compute the MassMantissa value by converting the Packed value to an Unsigned Int32 (and storing in a long),
                    //  then right shifting 11 bits by dividing by 2^11
                    // It would be more straightforward to use packedBasePeakInfo And CreateMask(11, 31) but VB won't let us
                    //  And a Currency value with a Long; this gives an OverFlow error
                    // We must divide the MassMantissa by 128 to get the mass

                    // Debug.Assert(unpackedMass == ExtractFromBitsInt32(packedBasePeakInfo, 11, 31) / 128f);
                    return (int)(NumberConversion.Int32ToUnsigned(packedBasePeakInfo) / 2048f) / 128f;      // 2048 = 2^11

                case 1:
                    // Standard data
                    // We must divide the MassMantissa by 1024 to get the mass
                    return (short)(packedBasePeakInfo & maskBPStandardDataMass) / 1024f;

                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                    // Uncalibrated data
                    // This type of data doesn't have a base peak mass
                    return 0;

                case 8:
                    // High intensity calibrated data
                    // Compute the MassMantissa value by converting the Packed value to an Unsigned Int32 (and storing in a long),
                    //  then right shifting 4 bits by dividing by 2^4
                    // We must divide the MassMantissa by 128 to get the mass

                    //Debug.Assert(unpackedMass == ExtractFromBitsInt32(packedBasePeakInfo, 4, 31) / 128f);
                    return (int)(NumberConversion.Int32ToUnsigned(packedBasePeakInfo) / 16f) / 128f;        // 16 = 2^4

                default:
                    return 0;
            }
        }
    }
}
