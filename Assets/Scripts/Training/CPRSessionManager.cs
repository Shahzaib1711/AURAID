using System;
using System.Collections;
using System.Text;
using AURAID.Voice;
using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Runs the 120-second practical CPR session when Practical_Panel is active.
    /// Opening voice is a single line (<see cref="PracticalSessionVoice.OpeningPracticalSession"/>) so we do not chain
    /// &quot;Hello… place hands&quot; with a second &quot;Hi… begin compressions&quot; clip.
    /// With <see cref="waitForStartButton"/>, a separate briefing line (<see cref="PracticalSessionVoice.BriefingScreenIntro"/>) plays when the briefing UI appears.
    /// Attach to Practical_Panel. Assign all references on the same GameObject.
    /// </summary>
    public class CPRSessionManager : MonoBehaviour
    {
        [Header("References (assign on Practical_Panel)")]
        [SerializeField] GloveInputManager gloveInput;
        [SerializeField] AssistanceEngine assistanceEngine;
        [SerializeField] PerformanceAnalyzer performanceAnalyzer;
        [SerializeField] VoiceController voiceController;
        [SerializeField] TrainingFlowManager flowManager;

        [Header("Session")]
        [SerializeField] float sessionDurationSec = 120f;
        [Tooltip("FSR / glove depth (cm) above this counts as compressions for coaching.")]
        [SerializeField, Min(0.01f)] float minDepthCmForActivity = 0.15f;

        [Header("Intro screen (optional)")]
        [Tooltip("When true, the timed session and opening voice start only after you call BeginPracticalSession() from your Start button.")]
        [SerializeField] bool waitForStartButton;
        [Tooltip("Hidden when Start is pressed (BeginPracticalSession). Shown again when this panel re-opens if Wait For Start Button is on.")]
        [SerializeField] GameObject introScreenRoot;

        float _sessionTimer;
        Coroutine _briefingVoiceCo;

        /// <summary>True when the practical flow shows a briefing / Start screen before the timed session (Inspector: Wait For Start Button).</summary>
        public bool WaitsForIntroStartButton => waitForStartButton;

        void Awake()
        {
            ResolveReferences();
        }

        void ResolveReferences()
        {
            if (gloveInput == null) gloveInput = GetComponent<GloveInputManager>();
            if (assistanceEngine == null) assistanceEngine = GetComponent<AssistanceEngine>();
            if (performanceAnalyzer == null) performanceAnalyzer = GetComponent<PerformanceAnalyzer>();
            if (voiceController == null) voiceController = GetComponent<VoiceController>();
            if (flowManager == null)
                flowManager = FindObjectOfType<TrainingFlowManager>();
        }

        bool _sessionActive;
        bool _sessionEndedOrSkipped;
        bool _handsPlaced;
        float _lastCompressionTime;
        const float CompressionDetectInterval = 0.4f;

        void OnEnable()
        {
            ResolveReferences();
            _sessionTimer = 0f;
            _sessionEndedOrSkipped = false;
            _handsPlaced = false;
            performanceAnalyzer?.Reset();
            assistanceEngine?.ResetConsecutive();

            if (voiceController != null)
                voiceController.StopSpeaking();

            if (waitForStartButton)
            {
                _sessionActive = false;
                if (introScreenRoot != null)
                    introScreenRoot.SetActive(true);
                PlayBriefingScreenVoice();
                return;
            }

            _sessionActive = true;
            PlayOpeningVoiceOnce();
        }

        /// <summary>
        /// Wire your intro <c>Start</c> button On Click here when <see cref="waitForStartButton"/> is enabled.
        /// </summary>
        public void BeginPracticalSession()
        {
            if (_sessionEndedOrSkipped || _sessionActive)
                return;
            ResolveReferences();
            if (_briefingVoiceCo != null)
            {
                StopCoroutine(_briefingVoiceCo);
                _briefingVoiceCo = null;
            }
            TrainingPracticalBriefingVoicePrefetch.Reset();
            if (voiceController != null)
                voiceController.StopSpeaking();
            _sessionActive = true;
            _sessionTimer = 0f;
            if (introScreenRoot != null)
                introScreenRoot.SetActive(false);
            PlayOpeningVoiceOnce();
        }

        void PlayBriefingScreenVoice()
        {
            if (!waitForStartButton)
                return;
            if (_briefingVoiceCo != null)
            {
                StopCoroutine(_briefingVoiceCo);
                _briefingVoiceCo = null;
            }
            _briefingVoiceCo = StartCoroutine(CoBriefingScreenTts());
        }

        IEnumerator CoBriefingScreenTts()
        {
            string name = TrainingManager.Instance != null ? TrainingManager.Instance.CurrentTraineeName : null;
            bool ar = PracticalSessionVoice.IsArabicTrainingLanguage();
            var prep = TrainingTtsText.Prepare(PracticalSessionVoice.BriefingScreenIntro(name, ar));
            string message = prep.text;
            string lang = prep.langCode;

            TrainingPracticalBriefingVoicePrefetch.EnsurePrefetchStarted(message, lang, this);

            var tts = FindObjectOfType<TtsClient>(true);
            if (tts == null)
            {
                if (voiceController != null)
                    voiceController.Speak(PracticalSessionVoice.BriefingScreenIntro(name, ar));
                _briefingVoiceCo = null;
                yield break;
            }

            AudioSource src = tts.GetComponent<AudioSource>();
            if (src == null)
                src = FindObjectOfType<AudioSource>(true);
            if (src == null)
            {
                if (voiceController != null)
                    voiceController.Speak(PracticalSessionVoice.BriefingScreenIntro(name, ar));
                _briefingVoiceCo = null;
                yield break;
            }

            byte[] receivedBytes = null;
            if (TrainingPracticalBriefingVoicePrefetch.TryTakePrepared(message, out var prefBytes))
                receivedBytes = prefBytes;
            else if (TrainingPracticalBriefingVoicePrefetch.CurrentKey == message && TrainingPracticalBriefingVoicePrefetch.InFlight)
            {
                float deadline = Time.realtimeSinceStartup + 25f;
                while (Time.realtimeSinceStartup < deadline && receivedBytes == null)
                {
                    if (TrainingPracticalBriefingVoicePrefetch.TryTakePrepared(message, out prefBytes))
                    {
                        receivedBytes = prefBytes;
                        break;
                    }
                    if (!TrainingPracticalBriefingVoicePrefetch.InFlight)
                        break;
                    yield return null;
                }
                if (receivedBytes == null)
                    TrainingPracticalBriefingVoicePrefetch.TryTakePrepared(message, out receivedBytes);
            }

            if (receivedBytes == null)
            {
                string errMsg = null;
                yield return tts.Synthesize(message, lang, b => receivedBytes = b, e => errMsg = e);
                if (!string.IsNullOrEmpty(errMsg))
                {
                    Debug.LogWarning("[CPRSessionManager] Briefing TTS: " + errMsg);
                    if (voiceController != null)
                        voiceController.Speak(PracticalSessionVoice.BriefingScreenIntro(name, ar));
                    _briefingVoiceCo = null;
                    yield break;
                }
            }

            if (receivedBytes == null || receivedBytes.Length < 16)
            {
                if (voiceController != null)
                    voiceController.Speak(PracticalSessionVoice.BriefingScreenIntro(name, ar));
                _briefingVoiceCo = null;
                yield break;
            }

            string h4 = Encoding.ASCII.GetString(receivedBytes, 0, 4);
            if (h4 != "RIFF")
            {
                if (voiceController != null)
                    voiceController.Speak(PracticalSessionVoice.BriefingScreenIntro(name, ar));
                _briefingVoiceCo = null;
                yield break;
            }

            AudioClip clip;
            try
            {
                clip = WavUtility.FromWavBytes(receivedBytes, "OpenAI_TTS_practical_briefing");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CPRSessionManager] Briefing WAV: " + e.Message);
                if (voiceController != null)
                    voiceController.Speak(PracticalSessionVoice.BriefingScreenIntro(name, ar));
                _briefingVoiceCo = null;
                yield break;
            }

            if (clip == null)
            {
                _briefingVoiceCo = null;
                yield break;
            }

            src.spatialBlend = 0f;
            src.Stop();
            src.clip = clip;
            src.Play();
            yield return new WaitForSeconds(clip.length);
            Destroy(clip);
            if (src != null && src.clip == clip)
                src.clip = null;
            _briefingVoiceCo = null;
        }

        void PlayOpeningVoiceOnce()
        {
            if (voiceController == null)
                return;
            TrainingPracticalVoicePrefetch.Reset();
            string name = TrainingManager.Instance != null ? TrainingManager.Instance.CurrentTraineeName : null;
            bool ar = PracticalSessionVoice.IsArabicTrainingLanguage();
            voiceController.Speak(PracticalSessionVoice.OpeningPracticalSession(name, ar));
        }

        void Update()
        {
            if (!_sessionActive) return;

            _sessionTimer += Time.deltaTime;

            if (gloveInput != null && gloveInput.currentDepth > minDepthCmForActivity)
            {
                if (!_handsPlaced)
                    _handsPlaced = true;

                if (Time.time - _lastCompressionTime >= CompressionDetectInterval)
                {
                    _lastCompressionTime = Time.time;
                    try
                    {
                        bool depthOk = assistanceEngine != null && assistanceEngine.IsValidDepth(gloveInput.currentDepth);
                        bool rateOk = assistanceEngine != null && assistanceEngine.IsValidRate(gloveInput.currentRate);
                        performanceAnalyzer?.RecordCompression(depthOk, rateOk, gloveInput.goodRecoil);
                        performanceAnalyzer?.RecordSample(gloveInput.currentDepth, gloveInput.currentRate);
                        assistanceEngine?.Evaluate(gloveInput.currentDepth, gloveInput.currentRate, gloveInput.goodRecoil);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[CPRSessionManager] Compression tick (ignored): " + ex.Message);
                    }
                }
            }

            if (_sessionTimer >= sessionDurationSec)
                EndSessionAndGoToQnAAfter();
        }

        void EndSessionAndGoToQnAAfter()
        {
            if (_sessionEndedOrSkipped)
                return;
            _sessionEndedOrSkipped = true;
            _sessionActive = false;
            if (voiceController != null)
            {
                string name = TrainingManager.Instance != null ? TrainingManager.Instance.CurrentTraineeName : null;
                bool ar = PracticalSessionVoice.IsArabicTrainingLanguage();
                voiceController.Speak(PracticalSessionVoice.SessionComplete(name, ar));
            }
            if (flowManager == null)
                flowManager = FindObjectOfType<TrainingFlowManager>();
            if (flowManager != null)
                flowManager.ShowQnAAfter();
            else
                Debug.LogWarning("[CPRSessionManager] TrainingFlowManager missing — assign Flow Manager on Practical_Panel or add TrainingFlowManager to the scene.");
        }

        void OnDisable()
        {
            if (_briefingVoiceCo != null)
            {
                StopCoroutine(_briefingVoiceCo);
                _briefingVoiceCo = null;
            }
            _sessionActive = false;
        }
    }
}
