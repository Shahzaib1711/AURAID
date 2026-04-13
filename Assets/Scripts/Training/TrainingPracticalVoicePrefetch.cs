using System.Collections;
using UnityEngine;
using AURAID.Voice;

namespace AURAID.Training
{
    /// <summary>
    /// Starts welcome-line TTS before the practical panel enables so network latency overlaps the transition.
    /// Consumed by VoiceController.Speak when the message matches.
    /// </summary>
    public static class TrainingPracticalVoicePrefetch
    {
        public static string CurrentKey { get; private set; }
        public static byte[] PreparedWav { get; private set; }
        public static bool InFlight { get; private set; }

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
            var tts = UnityEngine.Object.FindObjectOfType<TtsClient>();
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

        public static void Reset()
        {
            CurrentKey = null;
            PreparedWav = null;
            InFlight = false;
        }

        /// <summary>Clears prepared audio after handing bytes to playback (avoids replay).</summary>
        public static void ConsumePrepared()
        {
            PreparedWav = null;
            CurrentKey = null;
        }
    }
}
