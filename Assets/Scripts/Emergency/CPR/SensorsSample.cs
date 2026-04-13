using System;

namespace AURAID.Emergency.CPR
{
    /// <summary>
    /// One timestep for Emergency CPR guidance. Use FSR for depth; BNO (or IMU) for motion, rate hint, posture.
    /// </summary>
    [Serializable]
    public struct SensorsSample
    {
        public float time;

        /// <summary>FSR-402 normalized 0..1 (depth / force proxy).</summary>
        public float fsrForce;

        /// <summary>BNO055 (or IMU) motion magnitude 0..1 for compression rhythm / smart-assist.</summary>
        public float bnoMotion;

        /// <summary>Deviation from ideal “arms straight over chest” (degrees). -1 if unavailable.</summary>
        public float armTiltFromVerticalDeg;

        // LEGACY / NOT USED IN CURRENT EMERGENCY MODE — kept for compatibility with older streams, logging, or tools.
        public float heartRateBpm;
        public float spo2;
        public float motion;
        public bool responsive;
        public float respirationValue;
        public bool breathingDetected;
        public bool gaspingDetected;
        public bool cyanosisDetected;
    }
}
