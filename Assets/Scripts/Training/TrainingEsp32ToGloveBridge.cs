using System;
using System.Collections.Generic;
using AURAID.Emergency.CPR;
using AURAID.Integration;
using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Maps live <see cref="SensorsSample"/> from <see cref="ESP32TestReceiver"/> into
    /// <see cref="GloveInputManager.SetSample"/> so practical CPR uses real ESP32 sensor data.
    /// Put on Practical_Panel (or under practical root).
    /// </summary>
    public class TrainingEsp32ToGloveBridge : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] ESP32TestReceiver esp32;
        [SerializeField] GloveInputManager gloveInput;

        [Header("FSR → depth (cm)")]
        [Tooltip("Depth is always mapped from normalized FSR (0..1) so practical coaching runs even if emergency-style on/off thresholds are never crossed.")]
        [SerializeField] float depthCmAtFullForce = 7f;

        [Header("FSR smoothing (optional — off by default for stable demos)")]
        [Tooltip("Leave at 0 for the same behaviour as earlier builds. Raise only if you see jitter at high FSR and want gentler recoil / rate edges.")]
        [SerializeField, Range(0f, 1f)] float fsrInputSmoothing = 0f;

        [Header("Thresholds (compression counting for BPM / recoil only)")]
        [SerializeField, Tooltip("When FSR crosses this upward, a compression is counted for rate (not required for depth output).")]
        float compressionOnThreshold = 0.35f;
        [SerializeField] float compressionOffThreshold = 0.28f;
        [SerializeField] float recoilForceThreshold = 0.20f;

        [Header("Rate estimate")]
        [SerializeField, Tooltip("BPM when fewer than 2 compressions in the window.")]
        float defaultRateBpmWhenUnknown = 110f;

        readonly Queue<float> _compressionStarts = new Queue<float>();
        bool _inCompression;
        bool _recoilOk = true;
        float _lastRateBpm = 110f;
        float _fsrEma;
        bool _fsrEmaInit;

        void Awake()
        {
            if (gloveInput == null)
                gloveInput = GetComponent<GloveInputManager>();
            if (esp32 == null)
                esp32 = FindObjectOfType<ESP32TestReceiver>(true);
        }

        void OnEnable()
        {
            if (gloveInput == null)
                gloveInput = GetComponent<GloveInputManager>();
            if (esp32 == null)
                esp32 = FindObjectOfType<ESP32TestReceiver>(true);
            if (esp32 != null)
                esp32.OnSensorSample += OnSample;
            else
                Debug.LogWarning("[TrainingEsp32ToGloveBridge] No ESP32TestReceiver in scene — assign Esp32 on this component or add ESP32TestReceiver to the hierarchy.");
            gloveInput?.SetExternalStreamEnabled(true);
            _fsrEmaInit = false;
        }

        void OnDisable()
        {
            if (esp32 != null)
                esp32.OnSensorSample -= OnSample;
            gloveInput?.SetExternalStreamEnabled(false);
        }

        void OnSample(SensorsSample s)
        {
            try
            {
                OnSampleCore(s);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TrainingEsp32ToGloveBridge] OnSample (ignored): " + ex.Message);
            }
        }

        void OnSampleCore(SensorsSample s)
        {
            if (gloveInput == null)
                return;

            float f = TrainingSensorSafety.Clamp01(s.fsrForce);
            if (fsrInputSmoothing > 0f)
            {
                if (!_fsrEmaInit)
                {
                    _fsrEma = f;
                    _fsrEmaInit = true;
                }
                else
                    _fsrEma = Mathf.Lerp(_fsrEma, f, Mathf.Clamp01(fsrInputSmoothing));
                f = TrainingSensorSafety.Clamp01(_fsrEma);
            }

            float t = TrainingSensorSafety.IsFinite(s.time) ? s.time : Time.time;

            float onTh = Mathf.Clamp01(compressionOnThreshold);
            float offTh = Mathf.Clamp01(compressionOffThreshold);
            if (offTh >= onTh)
                offTh = Mathf.Max(0.05f, onTh - 0.07f);

            if (!_inCompression && f >= onTh)
            {
                _inCompression = true;
                _recoilOk = false;
                _compressionStarts.Enqueue(t);
                while (_compressionStarts.Count > 0 && t - _compressionStarts.Peek() > 10f)
                    _compressionStarts.Dequeue();
            }
            else if (_inCompression && f <= offTh)
            {
                _inCompression = false;
                _recoilOk = f <= Mathf.Clamp01(recoilForceThreshold);
            }

            float rate = EstimateRate(t);
            if (rate > 1f && TrainingSensorSafety.IsFinite(rate))
                _lastRateBpm = TrainingSensorSafety.ClampRateBpm(rate, defaultRateBpmWhenUnknown);

            float u = Mathf.Clamp01(f);
            float depthScale = Mathf.Clamp(depthCmAtFullForce, 0.5f, 15f);
            float depthCm = Mathf.Lerp(0f, depthScale, Mathf.Sqrt(u));

            float bpm = _compressionStarts.Count >= 2
                ? TrainingSensorSafety.ClampRateBpm(_lastRateBpm, defaultRateBpmWhenUnknown)
                : TrainingSensorSafety.ClampRateBpm(defaultRateBpmWhenUnknown, 110f);
            gloveInput.SetSample(depthCm, bpm, _recoilOk);
        }

        float EstimateRate(float now)
        {
            if (_compressionStarts.Count < 2)
                return 0f;
            float oldest = _compressionStarts.Peek();
            if (!TrainingSensorSafety.IsFinite(oldest) || !TrainingSensorSafety.IsFinite(now))
                return 0f;
            float window = Mathf.Max(0.1f, now - oldest);
            float bpm = (_compressionStarts.Count / window) * 60f;
            if (!TrainingSensorSafety.IsFinite(bpm))
                return 0f;
            return Mathf.Clamp(bpm, 30f, 220f);
        }
    }
}
