using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Validates depth/rate/recoil and gives voice guidance only after 3 consecutive
    /// incorrect compressions. Throttles repetition. Attach to Practical_Panel.
    /// </summary>
    public class AssistanceEngine : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] VoiceController voiceController;

        [Header("Validation")]
        [SerializeField] float validDepthMinCm = 5f;
        [SerializeField] float validDepthMaxCm = 6f;
        [SerializeField] float validRateMinBpm = 100f;
        [SerializeField] float validRateMaxBpm = 120f;

        [Header("Voice throttle (seconds between same-type corrections)")]
        [SerializeField] float sameMessageCooldownSec = 4f;

        int _consecutiveIncorrect;

        void Awake()
        {
            if (voiceController == null) voiceController = GetComponent<VoiceController>();
        }
        float _lastDepthCorrectionTime = -999f;
        float _lastRateCorrectionTime = -999f;
        float _lastRecoilCorrectionTime = -999f;

        public bool IsValidDepth(float depthCm)
        {
            return depthCm >= validDepthMinCm && depthCm <= validDepthMaxCm;
        }

        public bool IsValidRate(float rateBpm)
        {
            return rateBpm >= validRateMinBpm && rateBpm <= validRateMaxBpm;
        }

        /// <summary>Call each compression. Only speaks after 3 consecutive incorrect; respects cooldowns.</summary>
        public void Evaluate(float depthCm, float rateBpm, bool goodRecoil)
        {
            depthCm = TrainingSensorSafety.ClampDepthCm(depthCm);
            rateBpm = TrainingSensorSafety.ClampRateBpm(rateBpm, 110f);

            bool depthOk = IsValidDepth(depthCm);
            bool rateOk = IsValidRate(rateBpm);

            if (depthOk && rateOk && goodRecoil)
            {
                _consecutiveIncorrect = 0;
                return;
            }

            _consecutiveIncorrect++;
            if (_consecutiveIncorrect < 3) return;

            float t = Time.time;

            bool ar = PracticalSessionVoice.IsArabicTrainingLanguage();

            if (!depthOk && t - _lastDepthCorrectionTime >= sameMessageCooldownSec)
            {
                _lastDepthCorrectionTime = t;
                if (voiceController != null)
                    voiceController.Speak(depthCm < validDepthMinCm
                        ? PracticalSessionVoice.CoachingDepthTooLight(ar)
                        : PracticalSessionVoice.CoachingDepthTooHard(ar));
            }
            else if (!rateOk && t - _lastRateCorrectionTime >= sameMessageCooldownSec)
            {
                _lastRateCorrectionTime = t;
                if (voiceController != null)
                    voiceController.Speak(rateBpm < validRateMinBpm
                        ? PracticalSessionVoice.CoachingRateTooSlow(ar)
                        : PracticalSessionVoice.CoachingRateTooFast(ar));
            }
            else if (!goodRecoil && t - _lastRecoilCorrectionTime >= sameMessageCooldownSec)
            {
                _lastRecoilCorrectionTime = t;
                if (voiceController != null)
                    voiceController.Speak(PracticalSessionVoice.CoachingRecoil(ar));
            }
        }

        public void ResetConsecutive()
        {
            _consecutiveIncorrect = 0;
        }
    }
}
