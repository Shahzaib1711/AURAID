using System;
using System.Collections.Generic;
using AURAID.Emergency;
using UnityEngine;

namespace AURAID.Emergency.CPR
{
    /// <summary>
    /// Emergency CPR: only active when <see cref="EmergencyController.IsCprSensorFeedActive"/> is true.
    /// Uses FSR + BNO-style fields from <see cref="SensorsSample"/> for compression feedback (no SpO₂/HR/belt logic).
    /// </summary>
    public class RuleBasedCprAgent : MonoBehaviour
    {
        [Header("Config")]
        public CprRulesConfig config;

        [Header("Emergency flow")]
        [SerializeField] EmergencyController emergencyIntakeGate;

        public event Action<CprQuality, string> OnFeedback;

        readonly Queue<float> _compressionTimes = new Queue<float>();
        float _lastCompressionStart = -999f;
        float _lastFeedbackTime = -999f;
        bool _inCompression;
        bool _recoilOk = true;
        float _lastCompressionPeakTime = -999f;
        float _lastSmartAssistFireTime = -999f;

        public void ResetAgent()
        {
            _compressionTimes.Clear();
            _lastCompressionStart = -999f;
            _lastFeedbackTime = -999f;
            _inCompression = false;
            _recoilOk = true;
            _lastCompressionPeakTime = -999f;
            _lastSmartAssistFireTime = -999f;
        }

        public void UpdateConfig(CprRulesConfig newConfig)
        {
            config = newConfig;
            if (config != null)
                Debug.Log($"[RuleBasedCprAgent] UpdateConfig → Category: {config.patientCategory}, Context: {config.context}");
            ResetAgent();
        }

        void Awake()
        {
            if (emergencyIntakeGate == null)
                emergencyIntakeGate = GetComponentInParent<EmergencyController>();
        }

        EmergencyContext EffectiveContext =>
            config != null && config.context == EmergencyContext.Unknown
                ? EmergencyContext.Standard
                : (config != null ? config.context : EmergencyContext.Standard);

        public void PushSample(SensorsSample s)
        {
            if (config == null) return;
            if (emergencyIntakeGate != null && !emergencyIntakeGate.IsCprSensorFeedActive)
                return;

            float f = Clamp01Finite(s.fsrForce);
            float m1 = Clamp01Finite(s.bnoMotion);
            float m2 = Clamp01Finite(s.motion);
            float motion = Mathf.Max(m1, m2);
            float t = FiniteTimeOr(s.time);

            DetectCompression(t, f);
            SmartAssistMotion(t, motion);

            // Do not emit quality feedback before the first detected compression.
            // This prevents a misleading initial "Good compressions." prompt at session start.
            if (_compressionTimes.Count == 0 && !_inCompression)
                return;

            if (t - _lastFeedbackTime < config.feedbackIntervalSec)
                return;
            _lastFeedbackTime = t;

            var (q, msg) = EvaluateCompression(t, f, motion, SafeTiltDeg(s.armTiltFromVerticalDeg));
            OnFeedback?.Invoke(q, msg);
        }

        static float Clamp01Finite(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return 0f;
            return Mathf.Clamp01(v);
        }

        static float FiniteTimeOr(float time)
        {
            if (float.IsNaN(time) || float.IsInfinity(time))
                return Time.realtimeSinceStartup;
            return time;
        }

        static float SafeTiltDeg(float deg)
        {
            if (float.IsNaN(deg) || float.IsInfinity(deg)) return -1f;
            return Mathf.Clamp(deg, -180f, 180f);
        }

        void DetectCompression(float time, float f)
        {
            float onTh = Mathf.Clamp01(config.compressionOnThreshold);
            float offTh = Mathf.Clamp01(config.compressionOffThreshold);
            if (offTh >= onTh)
                offTh = Mathf.Max(0.05f, onTh - 0.12f);
            float recoilTh = Mathf.Clamp01(config.recoilForceThreshold);

            if (!_inCompression && f >= onTh)
            {
                _inCompression = true;
                _recoilOk = false;
                _lastCompressionStart = time;
                _lastCompressionPeakTime = time;
                _compressionTimes.Enqueue(time);
                while (_compressionTimes.Count > 0 && time - _compressionTimes.Peek() > 10f)
                    _compressionTimes.Dequeue();
            }
            else if (_inCompression && f <= offTh)
            {
                _inCompression = false;
                _recoilOk = f <= recoilTh;
            }

            if (f >= onTh * 0.85f)
                _lastCompressionPeakTime = time;
        }

        void SmartAssistMotion(float time, float motion)
        {
            if (config == null) return;
            if (time - _lastSmartAssistFireTime < 8f) return;
            if (time - _lastCompressionPeakTime < config.smartAssistPauseBeforeMotionSec)
                return;
            if (motion < config.unexpectedMotionDuringPause)
                return;

            _lastSmartAssistFireTime = time;
            OnFeedback?.Invoke(CprQuality.PossibleMovementCheckBreathing,
                "Possible movement detected. Pause compressions and check if the person is breathing.");
        }

        (CprQuality, string) EvaluateCompression(float now, float force, float motion, float armTiltDeg)
        {
            // Use last peak activity, not only latch start — sustained hard FSR (stuck "in compression") was refreshing
            // start once and then falsely tripping pause as "no compressions".
            float pauseRef = Mathf.Max(_lastCompressionPeakTime, _lastCompressionStart);
            if (pauseRef > 0.1f && now - pauseRef > config.maxAllowedPauseSec)
                return (CprQuality.PauseTooLong, "Keep steady compressions. Aim for 100 to 120 per minute.");

            float rate = EstimateRate(now);
            if (rate > 0f && !float.IsNaN(rate) && !float.IsInfinity(rate))
            {
                if (rate < config.minRatePerMin)
                    return (CprQuality.TooSlow, "Speed up. Aim for 100 to 120 compressions per minute.");
                if (rate > config.maxRatePerMin)
                    return (CprQuality.TooFast, "Slow down. Aim for 100 to 120 compressions per minute.");
            }

            if (_inCompression)
            {
                if (force < config.minForce)
                    return (CprQuality.TooLight, "Press harder.");
                if (force > config.maxForce)
                    return (CprQuality.TooHard, "Ease off slightly — still firm compressions.");
            }

            if (!_inCompression && !_recoilOk)
                return (CprQuality.IncompleteRecoil, "Allow full recoil between compressions.");

            if (armTiltDeg >= 0f && _inCompression)
            {
                float maxT = config.maxArmTiltDegrees;
                if (armTiltDeg > maxT)
                    return (CprQuality.ArmsBent,
                        "Adjust your posture. Straighten your arms — shoulders directly over your hands.");
                if (armTiltDeg > maxT * 0.55f)
                    return (CprQuality.PostureNudge,
                        "Adjust your posture. Position your shoulders over your hands.");
            }

            string technique = config.patientCategory switch
            {
                PatientCategory.Infant => "Good rhythm. Infant: two fingers, center of chest, about 4 cm depth.",
                PatientCategory.Child => "Good rhythm. Child: one or two hands as needed, about 5 cm depth.",
                _ => "Good compressions."
            };

            if (EffectiveContext == EmergencyContext.Drowning)
                technique += " Drowning: give rescue breaths when you can.";
            if (EffectiveContext == EmergencyContext.Pregnancy)
                technique += " Pregnancy: left uterine displacement if trained.";

            return (CprQuality.Good, technique);
        }

        float EstimateRate(float now)
        {
            if (_compressionTimes.Count < 2) return 0f;
            float oldest = _compressionTimes.Peek();
            if (float.IsNaN(oldest) || float.IsInfinity(oldest) || float.IsNaN(now) || float.IsInfinity(now))
                return 0f;
            float window = Mathf.Max(0.1f, now - oldest);
            float bpm = (_compressionTimes.Count / window) * 60f;
            if (float.IsNaN(bpm) || float.IsInfinity(bpm)) return 0f;
            return Mathf.Clamp(bpm, 30f, 220f);
        }
    }
}
