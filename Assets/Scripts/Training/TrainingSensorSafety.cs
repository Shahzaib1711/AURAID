using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Keeps FSR-derived training values finite and in-range so coaching and TTS never see NaN/Infinity.
    /// </summary>
    public static class TrainingSensorSafety
    {
        public static bool IsFinite(float v) => !(float.IsNaN(v) || float.IsInfinity(v));

        public static float ClampDepthCm(float depthCm)
        {
            if (!IsFinite(depthCm)) return 0f;
            return Mathf.Clamp(depthCm, 0f, 20f);
        }

        public static float ClampRateBpm(float rateBpm, float fallbackBpm = 110f)
        {
            if (!IsFinite(rateBpm)) return fallbackBpm;
            return Mathf.Clamp(rateBpm, 30f, 220f);
        }

        public static float Clamp01(float v)
        {
            if (!IsFinite(v)) return 0f;
            return Mathf.Clamp01(v);
        }
    }
}
