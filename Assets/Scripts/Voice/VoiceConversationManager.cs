using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AURAID.Voice
{
    public class VoiceConversationManager : MonoBehaviour
    {
        [Header("Refs")]
        public TtsClient tts;
        public AudioSource audioSource;
        [Header("STT (conversation)")]
        [Tooltip("Deepgram STT client. Assign to enable voice-in.")]
        public SttClient stt;
        [Tooltip("Microphone recorder. Assign to enable ListenAndTranscribe().")]
        public MicrophoneRecorder recorder;

        /// <summary>Fired when user speech is transcribed. Subscribe to build a conversation agent.</summary>
        public event Action<string> OnUserTranscript;

        [Header("Anti-spam")]
        [Tooltip("Hard minimum seconds between API calls.")]
        public float minSecondsBetweenSpeaks = 10f;

        [Tooltip("Minimum gap between starting TTS requests (reduces 429 bursts).")]
        public float minRequestGapSec = 1.2f;

        [Tooltip("If true, won't speak identical text twice in a row.")]
        public bool ignoreSameText = true;

        [Tooltip("If true, won't start a new TTS while audio is playing.")]
        public bool waitUntilAudioFinishes = true;

        [Header("429 Backoff")]
        public float backoffSecondsOn429 = 25f;

        [Header("Queue")]
        public int maxQueueSize = 2;

        [Header("Filter")]
        [Tooltip("If true, CPR 'Good' messages are not spoken (UI only).")]
        public bool muteGoodFeedback = true;

        [Header("Language")]
        [Tooltip("Default TTS language code (e.g. en, ar). Set to 'ar' for Arabic. Text containing Arabic script auto-uses Arabic.")]
        public string defaultTtsLang = "en";

        [Header("Playback (Quest / device)")]
        [Tooltip("Linear volume on the TTS AudioSource (0–1). Raise if emergency / intake voice is too quiet on headset.")]
        [Range(0f, 1f)]
        public float ttsVolume = 1f;
        [Tooltip("If true, plays as 2D (no distance falloff). Recommended for voice agent on Quest.")]
        public bool force2DVoice = true;

        private float _nextAllowedTime = 0f;
        private float _lastRequestTime = -999f;
        private string _lastSpokenText = "";
        private bool _processing = false;
        private bool _coolingOff429 = false;

        /// <summary>Cache: text -> AudioClip. Reduces TTS requests for repeated CPR phrases.</summary>
        private readonly Dictionary<string, AudioClip> _ttsCache = new Dictionary<string, AudioClip>(64);

        private readonly Queue<(string text, string lang, bool isFeedback)> _q
            = new Queue<(string, string, bool)>();

        void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
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

        // -----------------------------
        // Public API used by your app
        // -----------------------------
        public void AskUser(string question, string lang = null)
        {
            string resolved = ResolveLang(question, lang ?? defaultTtsLang);
            Enqueue(question, resolved, isFeedback: false);
        }

        /// <summary>
        /// Waits until <see cref="AskUser"/> / TTS queue has finished playing (including min gap between clips).
        /// Emergency intake must use this after each prompt; otherwise <see cref="audioSource"/> can be idle during
        /// queued delays and the next line is enqueued too early — with a small <see cref="maxQueueSize"/> older prompts are dropped.
        /// </summary>
        public IEnumerator WaitUntilSpeechPipelineIdle()
        {
            yield return null;
            while (_processing || _q.Count > 0 || (audioSource != null && audioSource.isPlaying))
                yield return null;
        }

        public void SpeakFeedback(string text, string lang = null)
        {
            // Filter common spam
            if (muteGoodFeedback && IsGoodMessage(text)) return;

            // Only speak critical feedback keywords (prevents spam)
            if (!IsCriticalCprFeedback(text)) return;

            string resolved = ResolveLang(text, lang ?? defaultTtsLang);
            Enqueue(text, resolved, isFeedback: true);
        }

        /// <summary>Use Arabic TTS when text contains Arabic script, so Arabic works even if caller passed "en".</summary>
        string ResolveLang(string text, string lang)
        {
            if (string.IsNullOrEmpty(text)) return lang ?? defaultTtsLang;
            if (ContainsArabic(text) && (string.IsNullOrEmpty(lang) || lang == "en"))
                return "ar";
            return lang ?? defaultTtsLang;
        }

        static bool ContainsArabic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if (c >= 0x0600 && c <= 0x06FF) return true;  // Arabic block
                if (c >= 0x0750 && c <= 0x077F) return true;  // Arabic Supplement
            }
            return false;
        }

        /// <summary>Record for the given seconds, send to Deepgram STT, then fire OnUserTranscript with the result. Call from a button or after TTS.</summary>
        public void ListenAndTranscribe(float seconds = 5f)
        {
            if (recorder == null) { Debug.LogError("[Voice] MicrophoneRecorder missing."); return; }
            if (stt == null) { Debug.LogError("[Voice] SttClient missing."); return; }
            StartCoroutine(DoListenAndTranscribe(seconds));
        }

        IEnumerator DoListenAndTranscribe(float seconds)
        {
            byte[] wavBytes = null;
            string wavError = null;

            yield return recorder.RecordForSeconds(
                seconds,
                wav => { wavBytes = wav; },
                err => { wavError = err; }
            );

            if (!string.IsNullOrEmpty(wavError))
            {
                Debug.LogError("[Voice] Record failed: " + wavError);
                yield break;
            }
            if (wavBytes == null || wavBytes.Length < 44)
            {
                Debug.LogWarning("[Voice] No audio captured.");
                yield break;
            }

            string transcript = null;
            string sttError = null;
            yield return stt.Transcribe(
                wavBytes,
                t => transcript = t,
                e => sttError = e
            );

            if (!string.IsNullOrEmpty(sttError))
            {
                Debug.LogError("[Voice] STT failed: " + sttError);
                yield break;
            }
            if (!string.IsNullOrEmpty(transcript))
            {
                Debug.Log("[Voice] User said: " + transcript);
                OnUserTranscript?.Invoke(transcript);
            }
        }

        // -----------------------------
        // Internals
        // -----------------------------
        void Enqueue(string text, string lang, bool isFeedback)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (ignoreSameText && text == _lastSpokenText) return;

            while (_q.Count >= maxQueueSize) _q.Dequeue(); // drop old spam
            _q.Enqueue((text, lang, isFeedback));

            if (!_processing)
                StartCoroutine(ProcessQueue());
        }

        IEnumerator PlayTtsBytesAsFile(byte[] audioBytes)
        {
            if (audioBytes == null || audioBytes.Length < 16)
            {
                Debug.LogError("[TTS] audioBytes empty or null.");
                yield break;
            }

            // 1) Check header so we don't decode JSON pretending it's WAV
            string h4 = Encoding.ASCII.GetString(audioBytes, 0, 4);
            string h12 = Encoding.ASCII.GetString(audioBytes, 0, Mathf.Min(12, audioBytes.Length));
            Debug.Log($"[TTS] Received {audioBytes.Length} bytes. Header={h12}");

            if (h4 != "RIFF")
            {
                string preview = Encoding.UTF8.GetString(audioBytes, 0, Mathf.Min(300, audioBytes.Length));
                Debug.LogError("[TTS] Not a WAV (no RIFF). Preview:\n" + preview);
                yield break;
            }

            // 2) Decode WAV in memory (avoids FMOD "Error loading file" when loading from file URI)
            AudioClip clip;
            try
            {
                clip = WavUtility.FromWavBytes(audioBytes, "OpenAI_TTS");
            }
            catch (Exception e)
            {
                Debug.LogError("[TTS] WAV decode failed: " + e.Message);
                yield break;
            }

            if (clip == null)
            {
                Debug.LogError("[TTS] WavUtility returned null clip.");
                yield break;
            }

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogError("[TTS] No AudioSource to play clip.");
                yield break;
            }

            audioSource.Stop();
            audioSource.clip = clip;
            ApplyTtsOutputSettings();
            audioSource.Play();
            Debug.Log("[TTS] Playing clip: " + clip.length + " sec");
        }

        IEnumerator ProcessQueue()
        {
            _processing = true;

            while (_q.Count > 0)
            {
                if (tts == null) { Debug.LogError("VoiceConversationManager: TtsClient missing."); break; }
                if (audioSource == null) { Debug.LogError("VoiceConversationManager: AudioSource missing."); break; }

                if (_coolingOff429)
                {
                    yield return new WaitForSeconds(backoffSecondsOn429);
                    _coolingOff429 = false;
                }

                // Hard pacing
                float wait = _nextAllowedTime - Time.time;
                if (wait > 0f) yield return new WaitForSeconds(wait);

                if (waitUntilAudioFinishes)
                    while (audioSource.isPlaying) yield return null;

                // Minimum gap between TTS requests (reduces 429 bursts)
                float gapWait = (_lastRequestTime + minRequestGapSec) - Time.time;
                if (gapWait > 0f) yield return new WaitForSeconds(gapWait);
                _lastRequestTime = Time.time;

                var item = _q.Dequeue();
                _lastSpokenText = item.text;
                _nextAllowedTime = Time.time + minSecondsBetweenSpeaks;

                // If cached, play instantly and skip API call
                if (_ttsCache.TryGetValue(item.text, out var cached) && cached != null)
                {
                    audioSource.Stop();
                    audioSource.clip = cached;
                    ApplyTtsOutputSettings();
                    audioSource.Play();
                    continue;
                }

                bool got429 = false;
                byte[] receivedBytes = null;

                yield return tts.Synthesize(
                    item.text, item.lang,
                    audioBytes =>
                    {
                        if (audioBytes == null || audioBytes.Length == 0) return;
                        int headerLen = Mathf.Min(12, audioBytes.Length);
                        string header = System.Text.Encoding.ASCII.GetString(audioBytes, 0, headerLen);
                        Debug.Log($"[TTS] Received {audioBytes.Length} bytes. Header={header}");
                        WavUtility.LogFmtInfo(audioBytes);

                        bool looksLikeWav = headerLen >= 12
                            && header.StartsWith("RIFF")
                            && System.Text.Encoding.ASCII.GetString(audioBytes, 8, 4) == "WAVE";
                        if (!looksLikeWav)
                            Debug.LogWarning("[TTS] Response does NOT look like WAV (expected RIFF....WAVE). You may be decoding JSON or an error body.");

                        receivedBytes = audioBytes;
                    },
                    err =>
                    {
                        Debug.LogError(err);
                        if (!string.IsNullOrEmpty(err) && err.Contains("429"))
                            got429 = true;
                    }
                );

                if (receivedBytes != null && receivedBytes.Length > 0)
                    yield return PlayTtsBytesAsFile(receivedBytes);

                if (got429)
                {
                    // Do NOT retry immediately; cool off and drop queued spam.
                    _coolingOff429 = true;
                    _q.Clear();
                }
            }

            _processing = false;
        }

        bool IsGoodMessage(string t)
        {
            if (string.IsNullOrEmpty(t)) return false;
            t = t.ToLowerInvariant();
            return t.Contains("good") || t.Contains("great") || t.Contains("perfect");
        }

        bool IsCriticalCprFeedback(string t)
        {
            if (string.IsNullOrEmpty(t)) return false;
            t = t.ToLowerInvariant();

            return t.Contains("too fast") ||
                   t.Contains("too slow") ||
                   t.Contains("push harder") ||
                   t.Contains("press harder") ||
                   t.Contains("allow recoil") ||
                   t.Contains("recoil") ||
                   t.Contains("no compressions") ||
                   t.Contains("resume") ||
                   t.Contains("continue");
        }
    }
}
