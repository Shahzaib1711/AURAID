using System.Collections;
using System.Text;
using AURAID.Voice;
using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Controls the visual flow inside the TrainingMode scene.
    /// One canvas / root is shown at a time in this order:
    /// Registration -> TrainingCanvas -> QnA (before) -> Practical -> QnA (after) -> Quiz -> Report.
    /// Practical and Report roots can be assigned later when they exist.
    /// </summary>
    public class TrainingFlowManager : MonoBehaviour
    {
        [Header("Training flow roots (assign in TrainingMode scene)")]
        [Tooltip("Registration canvas root (e.g. RegistrationCanvas).")]
        [SerializeField] GameObject registrationRoot;

        [Tooltip("Training info / instructions canvas (e.g. TrainingScreens). Only one child slide is shown at a time if it has TrainingScreensController.")]
        [SerializeField] GameObject trainingInfoRoot;

        [Tooltip("Q&A canvas shown BEFORE practical CPR (optional).")]
        [SerializeField] GameObject qnaBeforeRoot;

        [Tooltip("Practical CPR scenario root (MR mannequin, sensors). You will create this.")]
        [SerializeField] GameObject practicalRoot;

        [Tooltip("Q&A canvas shown AFTER practical CPR (optional). You can reuse the same as 'before'.")]
        [SerializeField] GameObject qnaAfterRoot;

        [Tooltip("Quiz canvas (e.g. QuizCanvas).")]
        [SerializeField] GameObject quizRoot;

        [Tooltip("Final report / summary root (you will create this).")]
        [SerializeField] GameObject reportRoot;

        TrainingScreensController _trainingScreensController;
        QnAPanelController _qnaPanelController;
        QuizScreensController _quizScreensController;
        Coroutine _readInstructionsVoiceCo;

        void Start()
        {
            if (registrationRoot == null)
                Debug.LogWarning("AURAID TrainingFlowManager: Registration Root is not assigned. Assign the Registration panel (child under your Training canvas) in the Inspector.");
            // When TrainingRoot becomes active (after user selects Training + CPR),
            // start by showing the Registration panel.
            ShowRegistration();
        }

        void ShowOnly(GameObject target)
        {
            if (registrationRoot != null) registrationRoot.SetActive(registrationRoot == target);
            if (trainingInfoRoot != null) trainingInfoRoot.SetActive(trainingInfoRoot == target);
            if (qnaBeforeRoot != null) qnaBeforeRoot.SetActive(qnaBeforeRoot == target);
            if (practicalRoot != null) practicalRoot.SetActive(practicalRoot == target);
            if (qnaAfterRoot != null) qnaAfterRoot.SetActive(qnaAfterRoot == target);
            if (quizRoot != null) quizRoot.SetActive(quizRoot == target);
            if (reportRoot != null) reportRoot.SetActive(reportRoot == target);
        }

        #region Public navigation API (wire these to buttons)

        /// <summary>Initial screen: registration (name + age).</summary>
        public void ShowRegistration()
        {
            if (registrationRoot == null)
                Debug.LogWarning("AURAID TrainingFlowManager: Registration Root is null. Assign RegistrationPanel (under UI > UICanvas) to Registration Root in the Inspector.");
            ShowOnly(registrationRoot);
        }

        /// <summary>After registration is complete. If TrainingImmersiveVideoController is present, it plays the video and advances to QnA; otherwise shows the usual info slides.</summary>
        public void ShowTrainingInfo()
        {
            bool ar = PracticalSessionVoice.IsArabicTrainingLanguage();
            var prep = TrainingTtsText.Prepare(TrainingScreensVoice.PleaseReadInstructionsCarefully(ar));
            TrainingInstructionsVoicePrefetch.EnsurePrefetchStarted(prep.text, prep.langCode, this);
            ShowOnly(trainingInfoRoot);
            PlayReadInstructionsVoicePrompt();
            var immersiveVideo = trainingInfoRoot != null ? trainingInfoRoot.GetComponentInChildren<TrainingImmersiveVideoController>(true) : null;
            if (immersiveVideo != null)
                return;
            if (_trainingScreensController == null && trainingInfoRoot != null)
                _trainingScreensController = trainingInfoRoot.GetComponentInChildren<TrainingScreensController>(true);
            _trainingScreensController?.ShowFirst();
        }

        /// <summary>Q&A before practical CPR (optional).</summary>
        public void ShowQnABefore()
        {
            ResolveQnAController(qnaBeforeRoot);
            _qnaPanelController?.SetLastSlideNextGoToPractical();
            ShowOnly(qnaBeforeRoot);
            _qnaPanelController?.ShowFirst();
        }

        /// <summary>Practical CPR stage (you control the content under this root).</summary>
        public void ShowPractical()
        {
            foreach (var q in FindObjectsOfType<QnAManager>())
                q.StopVoiceOutput();

            // Welcome TTS is owned only by CPRSessionManager (OnEnable or BeginPracticalSession). Prefetching here
            // duplicated "Hello … place your hands…" when Start was delayed or CPR was not found under practicalRoot.
            TrainingPracticalVoicePrefetch.Reset();

            if (practicalRoot != null)
            {
                var cpr = practicalRoot.GetComponentInChildren<CPRSessionManager>(true);
                if (cpr != null && cpr.WaitsForIntroStartButton)
                {
                    bool ar = PracticalSessionVoice.IsArabicTrainingLanguage();
                    string name = TrainingManager.Instance != null ? TrainingManager.Instance.CurrentTraineeName : null;
                    var prep = TrainingTtsText.Prepare(PracticalSessionVoice.BriefingScreenIntro(name, ar));
                    TrainingPracticalBriefingVoicePrefetch.EnsurePrefetchStarted(prep.text, prep.langCode, this);
                }
            }

            ShowOnly(practicalRoot);
        }

        /// <summary>Q&A after practical CPR (optional).</summary>
        public void ShowQnAAfter()
        {
            ResolveQnAController(qnaAfterRoot);
            _qnaPanelController?.SetLastSlideNextGoToQuiz();
            ShowOnly(qnaAfterRoot);
            _qnaPanelController?.ShowFirst();
        }

        void ResolveQnAController(GameObject root)
        {
            if (_qnaPanelController != null) return;
            if (root != null)
                _qnaPanelController = root.GetComponentInChildren<QnAPanelController>(true);
        }

        /// <summary>Quiz stage.</summary>
        public void ShowQuiz()
        {
            if (_quizScreensController == null && quizRoot != null)
                _quizScreensController = quizRoot.GetComponentInChildren<QuizScreensController>(true);
            ShowOnly(quizRoot);
            _quizScreensController?.ShowFirst();
        }

        /// <summary>Final report / summary stage.</summary>
        public void ShowReport() => ShowOnly(reportRoot);

        #endregion

        /// <summary>For UIFlowManager diagnostics only.</summary>
        public GameObject GetRegistrationRootForDebug() => registrationRoot;

        void PlayReadInstructionsVoicePrompt()
        {
            if (_readInstructionsVoiceCo != null)
            {
                StopCoroutine(_readInstructionsVoiceCo);
                _readInstructionsVoiceCo = null;
            }
            _readInstructionsVoiceCo = StartCoroutine(CoReadInstructionsTts());
        }

        IEnumerator CoReadInstructionsTts()
        {
            bool ar = PracticalSessionVoice.IsArabicTrainingLanguage();
            var prep = TrainingTtsText.Prepare(TrainingScreensVoice.PleaseReadInstructionsCarefully(ar));
            string message = prep.text;
            string lang = prep.langCode;

            TrainingInstructionsVoicePrefetch.EnsurePrefetchStarted(message, lang, this);

            var tts = FindObjectOfType<TtsClient>(true);
            if (tts == null)
            {
                Debug.LogWarning("AURAID TrainingFlowManager: TtsClient not found; cannot play read-instructions prompt.");
                _readInstructionsVoiceCo = null;
                yield break;
            }

            AudioSource src = tts.GetComponent<AudioSource>();
            if (src == null)
                src = FindObjectOfType<AudioSource>(true);
            if (src == null)
            {
                Debug.LogWarning("AURAID TrainingFlowManager: AudioSource not found for TTS.");
                _readInstructionsVoiceCo = null;
                yield break;
            }

            byte[] receivedBytes = null;
            if (TrainingInstructionsVoicePrefetch.TryTakePrepared(message, out var prefBytes))
                receivedBytes = prefBytes;
            else if (TrainingInstructionsVoicePrefetch.CurrentKey == message && TrainingInstructionsVoicePrefetch.InFlight)
            {
                float deadline = Time.realtimeSinceStartup + 25f;
                while (Time.realtimeSinceStartup < deadline && receivedBytes == null)
                {
                    if (TrainingInstructionsVoicePrefetch.TryTakePrepared(message, out prefBytes))
                    {
                        receivedBytes = prefBytes;
                        break;
                    }
                    if (!TrainingInstructionsVoicePrefetch.InFlight)
                        break;
                    yield return null;
                }
                if (receivedBytes == null)
                    TrainingInstructionsVoicePrefetch.TryTakePrepared(message, out receivedBytes);
            }

            if (receivedBytes == null)
            {
                string errMsg = null;
                yield return tts.Synthesize(message, lang, b => receivedBytes = b, e => errMsg = e);

                if (!string.IsNullOrEmpty(errMsg))
                {
                    Debug.LogWarning("AURAID TrainingFlowManager (read instructions TTS): " + errMsg);
                    _readInstructionsVoiceCo = null;
                    yield break;
                }
            }

            if (receivedBytes == null || receivedBytes.Length < 16)
            {
                _readInstructionsVoiceCo = null;
                yield break;
            }

            string h4 = Encoding.ASCII.GetString(receivedBytes, 0, 4);
            if (h4 != "RIFF")
            {
                Debug.LogWarning("AURAID TrainingFlowManager: TTS response not WAV (no RIFF).");
                _readInstructionsVoiceCo = null;
                yield break;
            }

            AudioClip clip;
            try
            {
                clip = WavUtility.FromWavBytes(receivedBytes, "OpenAI_TTS_training_info");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("AURAID TrainingFlowManager: WAV decode: " + e.Message);
                _readInstructionsVoiceCo = null;
                yield break;
            }

            if (clip == null)
            {
                _readInstructionsVoiceCo = null;
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
            _readInstructionsVoiceCo = null;
        }
    }
}

