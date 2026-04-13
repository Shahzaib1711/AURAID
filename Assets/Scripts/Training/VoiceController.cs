using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using AURAID.Voice;

namespace AURAID.Training
{
    /// <summary>
    /// Handles voice output for the practical CPR session. Uses the same TTS + WAV flow as
    /// Emergency VoiceRoot: TtsClient.Synthesize → WavUtility.FromWavBytes → AudioSource.Play.
    /// Attach to Practical_Panel. Assign or auto-finds TtsClient and AudioSource (e.g. from VoiceRoot).
    /// Welcome line can be prefetched from TrainingFlowManager so playback starts sooner.
    /// </summary>
    public class VoiceController : MonoBehaviour
    {
        [Header("TTS (same as Emergency VoiceRoot)")]
        [Tooltip("OpenAI TTS. Leave empty to find in scene (e.g. on Training VoiceRoot).")]
        [SerializeField] TtsClient tts;
        [Tooltip("Playback. Leave empty to find in scene.")]
        [SerializeField] AudioSource audioSource;
        [Tooltip("TTS language: en or ar.")]
        [SerializeField] string ttsLang = "en";

        [Header("Playback (Quest / device)")]
        [Tooltip("Linear volume on the TTS AudioSource (0–1). Raise here if voice is too quiet on headset.")]
        [Range(0f, 1f)]
        [SerializeField] float ttsVolume = 1f;
        [Tooltip("If true, plays as 2D (no distance falloff). Recommended for voice coach; disable only if you want positional 3D audio.")]
        [SerializeField] bool force2DVoice = true;

        bool _isSpeaking;
        Coroutine _speakCo;
        readonly Queue<string> _pendingRaw = new Queue<string>();

        void Awake() => EnsurePlaybackDependencies();

        /// <summary>
        /// Resolves TTS and playback when this component lives on a panel that starts disabled
        /// (so <see cref="Awake"/> never ran yet). Safe to call repeatedly.
        /// </summary>
        void EnsurePlaybackDependencies()
        {
            if (tts == null) tts = FindObjectOfType<TtsClient>(true);
            if (audioSource == null && tts != null) audioSource = tts.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = FindObjectOfType<AudioSource>(true);
            SyncTtsLangFromController();
            ApplyTtsOutputSettings();
        }

        void ApplyTtsOutputSettings()
        {
            if (audioSource == null) return;
            audioSource.volume = Mathf.Clamp01(ttsVolume);
            if (force2DVoice)
            {
                audioSource.spatialBlend = 0f;
                audioSource.panStereo = 0f;
            }
        }

        void Start()
        {
            SyncTtsLangFromController();
        }

        void SyncTtsLangFromController()
        {
            var langController = FindObjectOfType<TrainingLanguageController>();
            if (langController != null)
                ttsLang = langController.ttsLanguage;
        }

        /// <summary>Stops playback and the active speak coroutine. Does not cancel welcome prefetch (runs on TrainingFlowManager).</summary>
        public void StopSpeaking()
        {
            if (_speakCo != null)
            {
                StopCoroutine(_speakCo);
                _speakCo = null;
            }
            _isSpeaking = false;
            _pendingRaw.Clear();
            if (audioSource != null)
            {
                audioSource.Stop();
                if (audioSource.clip != null)
                {
                    Destroy(audioSource.clip);
                    audioSource.clip = null;
                }
            }
        }

        /// <summary>Speak a message. Uses prefetched WAV for the practical welcome when available. Localizes via TrainingTtsText.Prepare.</summary>
        public void Speak(string rawMessage)
        {
            if (string.IsNullOrEmpty(rawMessage)) return;
            EnsurePlaybackDependencies();
            if (_isSpeaking || _speakCo != null)
            {
                _pendingRaw.Enqueue(rawMessage);
                return;
            }

            StartSpeakInternal(rawMessage);
        }

        void StartSpeakInternal(string rawMessage)
        {
            var prep = TrainingTtsText.Prepare(rawMessage);
            ttsLang = prep.langCode;
            string finalMessage = prep.text;

            if (tts != null && audioSource != null)
            {
                if (TryStartFromPrefetchOrWait(finalMessage))
                    return;
                _speakCo = StartCoroutine(SpeakViaTts(finalMessage));
                return;
            }
            Debug.Log("[AURAID Voice] " + finalMessage);
            TryPlayNextQueued();
        }

        void TryPlayNextQueued()
        {
            if (_isSpeaking || _speakCo != null || _pendingRaw.Count == 0)
                return;
            StartSpeakInternal(_pendingRaw.Dequeue());
        }

        bool TryStartFromPrefetchOrWait(string finalMessage)
        {
            if (finalMessage != TrainingPracticalVoicePrefetch.CurrentKey)
                return false;

            if (TrainingPracticalVoicePrefetch.PreparedWav != null)
            {
                byte[] bytes = TrainingPracticalVoicePrefetch.PreparedWav;
                TrainingPracticalVoicePrefetch.ConsumePrepared();
                _speakCo = StartCoroutine(PlayFromWavBytes(bytes));
                return true;
            }

            if (TrainingPracticalVoicePrefetch.InFlight)
            {
                _speakCo = StartCoroutine(SpeakWaitPrefetchOrSynthesize(finalMessage));
                return true;
            }

            return false;
        }

        IEnumerator SpeakWaitPrefetchOrSynthesize(string finalMessage)
        {
            _isSpeaking = true;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (finalMessage != TrainingPracticalVoicePrefetch.CurrentKey)
                    break;
                if (TrainingPracticalVoicePrefetch.PreparedWav != null)
                {
                    byte[] b = TrainingPracticalVoicePrefetch.PreparedWav;
                    TrainingPracticalVoicePrefetch.ConsumePrepared();
                    yield return PlayFromWavBytesCore(b);
                    _speakCo = null;
                    TryPlayNextQueued();
                    yield break;
                }
                if (!TrainingPracticalVoicePrefetch.InFlight)
                    break;
                yield return null;
            }

            _isSpeaking = false;
            _speakCo = StartCoroutine(SpeakViaTts(finalMessage));
        }

        IEnumerator PlayFromWavBytes(byte[] receivedBytes)
        {
            _isSpeaking = true;
            yield return PlayFromWavBytesCore(receivedBytes);
            _speakCo = null;
            TryPlayNextQueued();
        }

        IEnumerator PlayFromWavBytesCore(byte[] receivedBytes)
        {
            if (receivedBytes == null || receivedBytes.Length < 16)
            {
                _isSpeaking = false;
                yield break;
            }

            string h4 = Encoding.ASCII.GetString(receivedBytes, 0, 4);
            if (h4 != "RIFF")
            {
                Debug.LogWarning("[AURAID Voice] TTS response not WAV (no RIFF).");
                _isSpeaking = false;
                yield break;
            }

            AudioClip clip = null;
            try
            {
                clip = WavUtility.FromWavBytes(receivedBytes, "OpenAI_TTS");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AURAID Voice] WAV decode: " + e.Message);
                _isSpeaking = false;
                yield break;
            }
            if (clip == null)
            {
                _isSpeaking = false;
                yield break;
            }

            audioSource.Stop();
            audioSource.clip = clip;
            ApplyTtsOutputSettings();
            audioSource.Play();
            yield return new WaitForSeconds(clip.length);
            Destroy(clip);
            if (audioSource != null && audioSource.clip == clip)
                audioSource.clip = null;
            _isSpeaking = false;
        }

        IEnumerator SpeakViaTts(string message)
        {
            _isSpeaking = true;
            byte[] receivedBytes = null;
            string errMsg = null;

            yield return tts.Synthesize(
                message, ttsLang,
                audioBytes => { if (audioBytes != null && audioBytes.Length > 0) receivedBytes = audioBytes; },
                err => errMsg = err
            );

            if (!string.IsNullOrEmpty(errMsg))
            {
                Debug.LogWarning("[AURAID Voice] TTS: " + errMsg);
                _isSpeaking = false;
                _speakCo = null;
                TryPlayNextQueued();
                yield break;
            }
            if (receivedBytes == null || receivedBytes.Length < 16)
            {
                _isSpeaking = false;
                _speakCo = null;
                TryPlayNextQueued();
                yield break;
            }

            string h4 = Encoding.ASCII.GetString(receivedBytes, 0, 4);
            if (h4 != "RIFF")
            {
                Debug.LogWarning("[AURAID Voice] TTS response not WAV (no RIFF).");
                _isSpeaking = false;
                _speakCo = null;
                TryPlayNextQueued();
                yield break;
            }

            AudioClip clip = null;
            try
            {
                clip = WavUtility.FromWavBytes(receivedBytes, "OpenAI_TTS");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AURAID Voice] WAV decode: " + e.Message);
                _isSpeaking = false;
                _speakCo = null;
                TryPlayNextQueued();
                yield break;
            }
            if (clip == null)
            {
                _isSpeaking = false;
                _speakCo = null;
                TryPlayNextQueued();
                yield break;
            }

            audioSource.Stop();
            audioSource.clip = clip;
            ApplyTtsOutputSettings();
            audioSource.Play();
            yield return new WaitForSeconds(clip.length);
            Destroy(clip);
            if (audioSource != null && audioSource.clip == clip)
                audioSource.clip = null;
            _isSpeaking = false;
            _speakCo = null;
            TryPlayNextQueued();
        }

        public bool IsSpeaking => _isSpeaking;
    }
}
