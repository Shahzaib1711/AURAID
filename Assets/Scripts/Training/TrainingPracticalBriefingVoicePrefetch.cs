using System.Collections;
using UnityEngine;
using AURAID.Voice;

namespace AURAID.Training
{
    /// <summary>
    /// Prefetches the practical briefing-line TTS while leaving Q&amp;A so it can play as soon as the briefing UI appears.
    /// </summary>
    public static class TrainingPracticalBriefingVoicePrefetch
    {
        public static string CurrentKey { get; private set; }
        public static byte[] PreparedWav { get; private set; }
        public static bool InFlight { get; private set; }

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

        public static bool TryTakePrepared(string finalTtsText, out byte[] wav)
        {
            wav = null;
            if (finalTtsText != CurrentKey || PreparedWav == null)
                return false;
            wav = PreparedWav;
            PreparedWav = null;
            CurrentKey = null;
            return true;
        }

        public static void Reset()
        {
            CurrentKey = null;
            PreparedWav = null;
            InFlight = false;
        }
    }
}
