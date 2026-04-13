using System;
using System.Text;
using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using AURAID.Voice;

namespace AURAID.Training
{
    /// <summary>
    /// Manages Q&amp;A conversation on the QnA Panel: message bubbles, keyword responses,
    /// and Hold to Speak (mic). Bot replies use same TTS + WAV flow as Emergency VoiceRoot.
    /// Wire MicButton: EventTrigger PointerDown -> StartHoldToSpeak, PointerUp -> StopHoldToSpeak.
    /// </summary>
    public class QnAManager : MonoBehaviour
    {
        [Header("Scroll content")]
        [Tooltip("The Content Transform under ConversationScrollView (or your scroll view). Messages are instantiated here.")]
        [SerializeField] Transform content;

        [Header("Message prefabs")]
        [Tooltip("Prefab for the user message bubble (must have TMP_Text in children).")]
        [SerializeField] GameObject userPrefab;
        [Tooltip("Prefab for the agent/assistant message bubble (must have TMP_Text in children).")]
        [SerializeField] GameObject agentPrefab;

        [Header("Response delay (seconds)")]
        [SerializeField] float responseDelaySec = 1f;

        [Header("TTS for bot replies (same as Emergency VoiceRoot)")]
        [Tooltip("OpenAI TTS. Leave empty to find in scene (e.g. on Training VoiceRoot).")]
        [SerializeField] TtsClient tts;
        [Tooltip("Playback. Leave empty to find in scene.")]
        [SerializeField] AudioSource ttsAudioSource;
        [SerializeField] string ttsLang = "en";
        [Tooltip("Linear volume on the QnA TTS AudioSource (0–1).")]
        [Range(0f, 1f)]
        [SerializeField] float ttsVolume = 1f;
        [Tooltip("2D playback avoids distance-based quieting on Quest.")]
        [SerializeField] bool force2DTts = true;

        [Header("LLM fallback (OpenAI Chat)")]
        [Tooltip("If true, use LLM when no keyword answer is found.")]
        [SerializeField] bool useLlmFallback = true;
        [Tooltip("OpenAI Chat client. Leave empty to find in scene.")]
        [SerializeField] AURAID.Voice.ChatClient chatClient;

        [Header("Hold to Speak (assign from VoiceRoot)")]
        [Tooltip("Microphone recorder for hold-to-speak. Leave empty to disable.")]
        [SerializeField] MicrophoneRecorder recorder;
        [Tooltip("STT client for transcribing. Leave empty to disable.")]
        [SerializeField] SttClient stt;

        [Tooltip("Record duration when using Tap to Speak (On Click).")]
        [SerializeField] float tapToSpeakDurationSec = 3f;

        [Header("Layout (conversation scroll)")]
        [Tooltip("Extra vertical space inside each bubble below the text (used by VerticalLayoutGroup sizing).")]
        [SerializeField] float bubbleVerticalPadding = 12f;
        [Tooltip("Extra pixels added to measured height so TMP overflow does not draw into the next row if estimates differ slightly.")]
        [SerializeField] float extraVerticalSafetyPx = 16f;
        [Tooltip("If viewport width is not ready on the same frame (common in XR), use this for wrapping until remeasure runs.")]
        [SerializeField] float fallbackWrapWidthPixels = 720f;
        [Tooltip("Turn off horizontal scrolling on the conversation ScrollRect (recommended for vertical chat).")]
        [SerializeField] bool disableConversationHorizontalScroll = true;

        [Header("Debug")]
        [SerializeField] bool logHoldToSpeak = true;

        bool _holdToSpeakRecording;

        void Awake()
        {
            if (recorder == null) recorder = FindObjectOfType<MicrophoneRecorder>();
            if (stt == null) stt = FindObjectOfType<SttClient>();
            if (tts == null) tts = FindObjectOfType<TtsClient>();
            if (ttsAudioSource == null && tts != null) ttsAudioSource = tts.GetComponent<AudioSource>();
            if (ttsAudioSource == null) ttsAudioSource = FindObjectOfType<AudioSource>();
            if (chatClient == null) chatClient = FindObjectOfType<AURAID.Voice.ChatClient>();
            if (logHoldToSpeak && (recorder == null || stt == null))
                Debug.Log("[AURAID QnA] Hold to Speak: assign Recorder and Stt on QnA Panel, or add MicrophoneRecorder and SttClient to the scene (e.g. on VoiceRoot).");
            ApplyTtsOutputSettings();
            ConfigureConversationScrollIfNeeded();
        }

        void ConfigureConversationScrollIfNeeded()
        {
            if (content == null || !disableConversationHorizontalScroll) return;
            var scroll = content.GetComponentInParent<ScrollRect>();
            if (scroll == null) return;
            scroll.horizontal = false;
            scroll.vertical = true;
        }

        void ApplyTtsOutputSettings()
        {
            if (ttsAudioSource == null) return;
            ttsAudioSource.volume = Mathf.Clamp01(ttsVolume);
            if (force2DTts)
            {
                ttsAudioSource.spatialBlend = 0f;
                ttsAudioSource.panStereo = 0f;
            }
        }

        void Start()
        {
            var langController = FindObjectOfType<TrainingLanguageController>();
            if (langController != null)
                ttsLang = langController.ttsLanguage;
        }

        /// <summary>Stops bot TTS and related coroutines when leaving Q&amp;A (e.g. opening practical) so the next screen is not delayed or drowned out.</summary>
        public void StopVoiceOutput()
        {
            StopAllCoroutines();
            if (ttsAudioSource != null)
            {
                ttsAudioSource.Stop();
                if (ttsAudioSource.clip != null)
                {
                    Destroy(ttsAudioSource.clip);
                    ttsAudioSource.clip = null;
                }
            }
        }

        /// <summary>Call from MicButton PointerDown (EventTrigger). Starts recording.</summary>
        public void StartHoldToSpeak()
        {
            if (logHoldToSpeak) Debug.Log("[AURAID QnA] StartHoldToSpeak called.");
            if (recorder == null) { if (logHoldToSpeak) Debug.LogWarning("[AURAID QnA] Recorder not assigned."); return; }
            if (stt == null) { if (logHoldToSpeak) Debug.LogWarning("[AURAID QnA] Stt not assigned."); return; }
            if (_holdToSpeakRecording) return;
            _holdToSpeakRecording = true;
            recorder.StartRecording();
        }

        /// <summary>Call from MicButton PointerUp (EventTrigger). Stops, transcribes, then AskQuestion(transcript).</summary>
        public void StopHoldToSpeak()
        {
            if (logHoldToSpeak) Debug.Log("[AURAID QnA] StopHoldToSpeak called.");
            if (!_holdToSpeakRecording || recorder == null) return;
            _holdToSpeakRecording = false;
            byte[] wav = recorder.StopRecording();
            if (wav == null || wav.Length < 44) { if (logHoldToSpeak) Debug.LogWarning("[AURAID QnA] No audio captured (hold too short?)."); return; }
            if (stt == null) return;
            StartCoroutine(TranscribeAndAsk(wav));
        }

        /// <summary>Tap to speak: record for a fixed duration then transcribe. Wire to Button On Click if Pointer Down/Up don't work (e.g. XR).</summary>
        public void TapToSpeak()
        {
            if (recorder == null || stt == null) return;
            StartCoroutine(RecordForSecondsThenTranscribe(tapToSpeakDurationSec));
        }

        IEnumerator RecordForSecondsThenTranscribe(float seconds)
        {
            recorder.StartRecording();
            yield return new WaitForSecondsRealtime(seconds);
            byte[] wav = recorder.StopRecording();
            if (wav != null && wav.Length >= 44)
                StartCoroutine(TranscribeAndAsk(wav));
        }

        IEnumerator TranscribeAndAsk(byte[] wavBytes)
        {
            string transcript = null;
            string error = null;
            yield return stt.Transcribe(wavBytes, t => transcript = t, e => error = e);
            if (!string.IsNullOrWhiteSpace(error))
                Debug.LogWarning("[AURAID QnA] " + error);
            if (!string.IsNullOrWhiteSpace(transcript))
                AskQuestion(transcript);
        }

        /// <summary>Add user question and generate agent response after a short delay.</summary>
        public void AskQuestion(string question)
        {
            if (string.IsNullOrWhiteSpace(question)) return;
            AddUserMessage(question);
            StartCoroutine(GenerateResponse(question));
        }

        void AddUserMessage(string text)
        {
            if (content == null || userPrefab == null) return;
            GameObject msg = Instantiate(userPrefab, content);
            var tmp = msg.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
                ApplyMessageBubbleLayout(msg, tmp, text);
            RefreshConversationLayout(msg);
            StartCoroutine(FinalizeBubbleLayoutNextFrame(msg));
        }

        void AddAgentMessage(string text)
        {
            if (content == null || agentPrefab == null) return;
            GameObject msg = Instantiate(agentPrefab, content);
            var tmp = msg.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
                ApplyMessageBubbleLayout(msg, tmp, text);
            RefreshConversationLayout(msg);
            StartCoroutine(FinalizeBubbleLayoutNextFrame(msg));
        }

        /// <summary>
        /// Sets wrapped text height from measured preferred size (avoids nested ContentSizeFitter + TMP bugs and zero-width first frames in XR).
        /// </summary>
        void ApplyMessageBubbleLayout(GameObject bubbleRoot, TMP_Text tmp, string text)
        {
            tmp.text = text;
            tmp.enableWordWrapping = true;

            Canvas.ForceUpdateCanvases();

            float wrapWidth = ResolveWrapWidthFor(tmp);
            tmp.ForceMeshUpdate(true);

            Vector2 preferred = tmp.GetPreferredValues(text, wrapWidth, 0);
            float boundsH = tmp.textBounds.size.y;
            float textHeight = Mathf.Max(preferred.y, boundsH, 28f) + extraVerticalSafetyPx;

            var textRt = tmp.rectTransform;
            textRt.sizeDelta = new Vector2(textRt.sizeDelta.x, textHeight);

            // After the rect exists, TMP mesh can differ slightly — take a second reading.
            tmp.ForceMeshUpdate(true);
            boundsH = tmp.textBounds.size.y;
            textHeight = Mathf.Max(textHeight, boundsH + extraVerticalSafetyPx);
            textRt.sizeDelta = new Vector2(textRt.sizeDelta.x, textHeight);

            var bubbleLe = bubbleRoot.GetComponent<LayoutElement>();
            if (bubbleLe == null)
                bubbleLe = bubbleRoot.AddComponent<LayoutElement>();
            float bubbleH = textHeight + bubbleVerticalPadding;
            bubbleLe.minHeight = bubbleH;
            bubbleLe.preferredHeight = bubbleH;
            bubbleLe.flexibleHeight = 0f;
        }

        /// <summary>
        /// Uses the live text rect width when it is valid; otherwise viewport-based estimate. Improves wrapping vs. LLM long replies.
        /// </summary>
        float ResolveWrapWidthFor(TMP_Text tmp)
        {
            var tr = tmp.rectTransform;
            float live = Mathf.Abs(tr.rect.width);
            float fromViewport = GetQnATextWrapWidthFromViewport();
            if (live > 80f)
                return live;
            return Mathf.Max(fromViewport, 120f);
        }

        float GetQnATextWrapWidthFromViewport()
        {
            float w = 0f;
            var scroll = content != null ? content.GetComponentInParent<ScrollRect>() : null;
            if (scroll != null && scroll.viewport != null)
                w = scroll.viewport.rect.width;
            if (w < 2f && content is RectTransform contentRt)
                w = contentRt.rect.width;

            var vlg = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
            if (vlg != null)
                w -= (vlg.padding.left + vlg.padding.right);

            // Prefab TMP uses horizontal sizeDelta -40 vs bubble (20 px per side).
            w -= 40f;

            if (w < 120f)
                w = fallbackWrapWidthPixels;
            return w;
        }

        /// <summary>
        /// Re-measures every bubble so earlier rows update when the viewport finally has a stable width (Quest / XR).
        /// </summary>
        void ReflowAllConversationBubbles()
        {
            if (content == null) return;
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                var tmp = child.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null)
                    ApplyMessageBubbleLayout(child.gameObject, tmp, tmp.text);
            }
        }

        IEnumerator FinalizeBubbleLayoutNextFrame(GameObject bubble)
        {
            // First frame: layout / canvas scale often still settling on device.
            yield return null;
            yield return null;
            if (bubble == null) yield break;
            ReflowAllConversationBubbles();
            RefreshConversationLayout(null);
        }

        /// <summary>
        /// Rebuilds scroll content so stacked bubbles get correct heights (avoids TMP drawing past a fixed rect and overlapping the next row).
        /// </summary>
        void RefreshConversationLayout(GameObject lastAddedBubble)
        {
            if (content == null) return;
            var contentRt = content as RectTransform;
            if (contentRt == null) return;

            Canvas.ForceUpdateCanvases();
            if (lastAddedBubble != null)
            {
                var bubbleRt = lastAddedBubble.GetComponent<RectTransform>();
                if (bubbleRt != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRt);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);

            var scroll = content.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 0f;
            }
        }

        IEnumerator GenerateResponse(string question)
        {
            yield return new WaitForSeconds(responseDelaySec);

            bool fromLlm = false;
            string response;

            // 1) Try fast keyword answer first.
            if (!TryGetKeywordResponse(question, out response))
            {
                // 2) Optional LLM fallback for free-form questions.
                if (useLlmFallback && chatClient != null)
                {
                    string llmAnswer = null;
                    string llmError = null;
                    yield return chatClient.Ask(
                        question,
                        ttsLang,
                        ans => llmAnswer = ans,
                        err => llmError = err
                    );
                    if (!string.IsNullOrWhiteSpace(llmError))
                        Debug.LogWarning("[AURAID QnA] LLM: " + llmError);
                    if (!string.IsNullOrWhiteSpace(llmAnswer))
                    {
                        response = llmAnswer;
                        fromLlm = true;
                    }
                }

                // 3) Final safety fallback if LLM missing or failed.
                if (string.IsNullOrWhiteSpace(response))
                    response = GetDefaultFallback();
            }

            // 4) Arabic mapping only for our canned English phrases.
            if (ttsLang == "ar" && !fromLlm)
                response = TrainingLocalization.ToArabic(response);

            AddAgentMessage(response);

            if (TrainingManager.Instance != null)
                TrainingManager.Instance.SaveQnAExchange(question, response);

            if (tts != null && ttsAudioSource != null && !string.IsNullOrEmpty(response))
                StartCoroutine(SpeakResponseViaTts(response));
        }

        /// <summary>Same WAV flow as Emergency VoiceRoot: Synthesize → RIFF check → WavUtility.FromWavBytes → Play.</summary>
        IEnumerator SpeakResponseViaTts(string text)
        {
            byte[] receivedBytes = null;
            string errMsg = null;
            yield return tts.Synthesize(
                text, ttsLang,
                audioBytes => { if (audioBytes != null && audioBytes.Length > 0) receivedBytes = audioBytes; },
                err => errMsg = err
            );
            if (!string.IsNullOrEmpty(errMsg)) { Debug.LogWarning("[AURAID QnA] TTS: " + errMsg); yield break; }
            if (receivedBytes == null || receivedBytes.Length < 16) yield break;
            if (Encoding.ASCII.GetString(receivedBytes, 0, 4) != "RIFF") yield break;
            AudioClip clip = null;
            try { clip = WavUtility.FromWavBytes(receivedBytes, "OpenAI_TTS"); }
            catch (Exception e) { Debug.LogWarning("[AURAID QnA] WAV: " + e.Message); yield break; }
            if (clip == null) yield break;
            ttsAudioSource.Stop();
            ttsAudioSource.clip = clip;
            ApplyTtsOutputSettings();
            ttsAudioSource.Play();
            while (ttsAudioSource != null && ttsAudioSource.isPlaying)
                yield return null;
            Destroy(clip);
        }

        bool TryGetKeywordResponse(string question, out string response)
        {
            response = null;
            if (string.IsNullOrWhiteSpace(question))
            {
                response = GetDefaultFallback();
                return true;
            }

            string q = question.ToLowerInvariant();

            if (q.Contains("depth"))
            {
                response = "Chest compressions should be 5 to 6 centimeters deep.";
                return true;
            }
            if (q.Contains("rate") || q.Contains("speed") || q.Contains("per minute"))
            {
                response = "Maintain a compression rate between 100 and 120 per minute.";
                return true;
            }
            if (q.Contains("stop") || q.Contains("when do i stop") || q.Contains("how long"))
            {
                response = "Continue CPR until emergency responders arrive or the person shows clear signs of life.";
                return true;
            }

            return false;
        }

        string GetDefaultFallback()
        {
            return "Focus on maintaining correct compression depth and rhythm.";
        }
    }
}

