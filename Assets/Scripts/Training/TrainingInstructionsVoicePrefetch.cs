using System.Collections;
using UnityEngine;
using AURAID.Voice;

namespace AURAID.Training
{
    /// <summary>
    /// Prefetches the &quot;read the instructions&quot; TTS while registration / session creation runs,
    /// so playback can start as soon as the training info screen is shown.
    /// </summary>
    public static class TrainingInstructionsVoicePrefetch
    {
        public static string CurrentKey { get; private set; }
        public static byte[] PreparedWav { get; private set; }
        public static bool InFlight { get; private set; }

        /// <summary>
        /// Starts a prefetch only if we do not already have WAV bytes or an in-flight request for the same final text.
        /// </summary>
        public static void EnsurePrefetchStarted(string finalTtsText, string langCode, MonoBehaviour coroutineHost)
        {
            if (coroutineHost == null || string.IsNullOrEmpty(finalTtsText))
                return;
            if (CurrentKey == finalTtsText && PreparedWav != null)
                return;
            if (CurrentKey == finalTtsText && InFlight)
                return;
            StartRequest(finalTtsText, langCode, coroutineHost);
        }

        public static void StartRequest(string finalTtsText, string langCode, MonoBehaviour coroutineHost)
        {
            Reset();
            if (string.IsNullOrEmpty(finalTtsText) || coroutineHost == null)
                return;
            CurrentKey = finalTtsText;
            InFlight = true;
            coroutineHost.StartCoroutine(CoFetch(finalTtsText, langCode));
        }

        static IEnumerator CoFetch(string final, string lang)
        {
            var tts = Object.FindObjectOfType<TtsClient>(true);
            if (tts == null)
            {
                InFlight = false;
                yield break;
            }

            byte[] received = null;
            string err = null;
            yield return tts.Synthesize(
                final, lang,
                b => { if (b != null && b.Length > 0) received = b; },
                e => err = e
            );

            if (string.IsNullOrEmpty(err) && received != null && received.Length >= 16)
                PreparedWav = received;
            InFlight = false;
        }

        /// <summary>True if prepared audio matches <paramref name="finalTtsText"/>; assigns bytes and clears prefetch state.</summary>
        public static bool TryTakePrepared(string finalTtsText, out byte[] wav)
        {
            wav = null;
            if (finalTtsText != CurrentKey || PreparedWav == null)
                return false;
            wav = PreparedWav;
            ConsumePrepared();
            return true;
        }

        public static void Reset()
        {
            CurrentKey = null;
            PreparedWav = null;
            InFlight = false;
        }

        static void ConsumePrepared()
        {
            PreparedWav = null;
            CurrentKey = null;
        }
    }
}
