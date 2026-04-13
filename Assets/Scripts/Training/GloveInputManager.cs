using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Provides compression depth, rate, and recoil from gloves (or simulated in editor).
    /// Use "Sample data for demo" to trigger voice feedback without hardware. Attach to Practical_Panel.
    /// </summary>
    public class GloveInputManager : MonoBehaviour
    {
        public enum SampleDataPreset
        {
            Off,
            TooShallow,
            TooDeep,
            TooSlow,
            TooFast,
            BadRecoil,
            CycleAll
        }

        [Header("Editor simulation (depth 4–7 cm, rate 100–120 BPM)")]
        [SerializeField, Range(4f, 7f)] float simulatedDepthCm = 5.5f;
        [SerializeField, Range(100f, 120f)] float simulatedRateBpm = 110f;
        [SerializeField] bool simulatedGoodRecoil = true;

        [Header("Sample data for demo (makes agent speak)")]
        [Tooltip("When not Off, overrides simulation with values that trigger voice feedback after 3 compressions.")]
        [SerializeField] SampleDataPreset sampleDataPreset = SampleDataPreset.Off;
        [Tooltip("For CycleAll: seconds before switching to next preset.")]
        [SerializeField] float cycleIntervalSec = 12f;

        [Header("External stream (ESP32 bridge)")]
        [Tooltip("When true, use only data from SetSample() (ESP32 stream). No sliders/sample presets.")]
        [SerializeField] bool useExternalStream;

        float _phase;
        float _lastCompressionTime;
        int _cycleIndex;
        float _cycleSwitchTime;

        public float currentDepth { get; private set; }
        public float currentRate { get; private set; }
        public bool goodRecoil { get; private set; }

        void Update()
        {
#if UNITY_EDITOR
            SimulateEditorInput();
#else
            UpdateFromHardware();
#endif
        }

#if UNITY_EDITOR
        void SimulateEditorInput()
        {
            if (useExternalStream)
                return;

            float depthCm = simulatedDepthCm;
            float rateBpm = simulatedRateBpm;
            bool recoil = simulatedGoodRecoil;

            if (sampleDataPreset != SampleDataPreset.Off)
            {
                SampleDataPreset active = sampleDataPreset;
                if (sampleDataPreset == SampleDataPreset.CycleAll)
                {
                    if (Time.time - _cycleSwitchTime >= cycleIntervalSec)
                    {
                        _cycleSwitchTime = Time.time;
                        _cycleIndex = (_cycleIndex + 1) % 5;
                    }
                    active = (SampleDataPreset)(_cycleIndex + 1);
                }
                ApplySamplePreset(active, ref depthCm, ref rateBpm, ref recoil);
            }

            float t = Time.time;
            float interval = 60f / Mathf.Clamp(rateBpm, 80f, 140f);
            if (t - _lastCompressionTime >= interval)
            {
                _lastCompressionTime = t;
                _phase += 1f;
            }
            float cycle = Mathf.Sin((t - _lastCompressionTime) / interval * Mathf.PI);
            currentDepth = cycle > 0.3f ? Mathf.Clamp(depthCm + Random.Range(-0.2f, 0.2f), 2f, 8f) : 0f;
            currentRate = rateBpm;
            goodRecoil = recoil;
        }

        void ApplySamplePreset(SampleDataPreset preset, ref float depthCm, ref float rateBpm, ref bool recoil)
        {
            switch (preset)
            {
                case SampleDataPreset.TooShallow:
                    depthCm = 3.5f;
                    rateBpm = 110f;
                    recoil = true;
                    break;
                case SampleDataPreset.TooDeep:
                    depthCm = 7f;
                    rateBpm = 110f;
                    recoil = true;
                    break;
                case SampleDataPreset.TooSlow:
                    depthCm = 5.5f;
                    rateBpm = 80f;
                    recoil = true;
                    break;
                case SampleDataPreset.TooFast:
                    depthCm = 5.5f;
                    rateBpm = 130f;
                    recoil = true;
                    break;
                case SampleDataPreset.BadRecoil:
                    depthCm = 5.5f;
                    rateBpm = 110f;
                    recoil = false;
                    break;
                default:
                    break;
            }
        }
#endif

        void UpdateFromHardware()
        {
            // Placeholder for real hardware: set currentDepth, currentRate, goodRecoil from sensors.
        }

        /// <summary>Called by ESP32/hardware bridge to push a sample.</summary>
        public void SetSample(float depthCm, float rateBpm, bool recoilOk)
        {
            currentDepth = TrainingSensorSafety.ClampDepthCm(depthCm);
            currentRate = TrainingSensorSafety.ClampRateBpm(rateBpm, 110f);
            goodRecoil = recoilOk;
        }

        /// <summary>Training: when using <see cref="SetSample"/> from ESP32 / live stream, enable so Editor simulation does not override.</summary>
        public void SetExternalStreamEnabled(bool enabled) => useExternalStream = enabled;
    }
}
