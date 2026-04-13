using UnityEngine;

namespace AURAID.Emergency.CPR
{
    [CreateAssetMenu(menuName = "AURAID/Emergency/CPR Rules Config")]
    public class CprRulesConfig : ScriptableObject
    {
        [Header("Patient + situation (from triage voice / UI)")]
        public PatientCategory patientCategory = PatientCategory.Adult;
        public EmergencyContext context = EmergencyContext.Standard;

        [Header("FSR compression")]
        public float compressionOnThreshold = 0.65f;
        public float compressionOffThreshold = 0.35f;
        public float recoilForceThreshold = 0.20f;

        [Header("Depth / rate targets")]
        public float minRatePerMin = 100f;
        public float maxRatePerMin = 120f;
        public float minForce = 0.75f;
        public float maxForce = 0.95f;

        [Header("Feedback timing")]
        public float feedbackIntervalSec = 0.5f;
        public float maxAllowedPauseSec = 2.5f;

        [Header("BNO055 / posture")]
        [Tooltip("If arm tilt (deg) is above this while compressing, suggest straightening arms.")]
        public float maxArmTiltDegrees = 28f;
        [Tooltip("Motion magnitude 0..1 above this during a long pause may trigger “check breathing”.")]
        public float unexpectedMotionDuringPause = 0.35f;
        [Tooltip("Seconds without compression before smart-assist watches for motion.")]
        public float smartAssistPauseBeforeMotionSec = 2f;
    }
}
