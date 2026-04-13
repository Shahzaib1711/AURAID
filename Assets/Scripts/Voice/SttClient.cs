using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace AURAID.Voice
{
    /// <summary>
    /// Speech-to-text using Deepgram's pre-recorded REST API.
    /// POST WAV (or other supported format) to /v1/listen, get back transcript.
    /// </summary>
    public class SttClient : MonoBehaviour
    {
        [Header("Deepgram")]
        public string listenUrl = "https://api.deepgram.com/v1/listen";
        [Tooltip("DO NOT ship with API key in production. Use a backend.")]
        public string apiKey = "";

        [Header("Options")]
        [Tooltip("Model: nova-2, base, etc. See Deepgram docs.")]
        public string model = "nova-2";
        public string language = "en";
        [Tooltip("Add punctuation and capitalization.")]
        public bool punctuate = true;

        [Serializable]
        class DeepgramResponse
        {
            public DeepgramResults results;
        }

        [Serializable]
        class DeepgramResults
        {
            public DeepgramChannel[] channels;
        }

        [Serializable]
        class DeepgramChannel
        {
            public DeepgramAlternative[] alternatives;
        }

        [Serializable]
        class DeepgramAlternative
        {
            public string transcript;
            public float confidence;
        }

        /// <summary>
        /// Transcribe WAV bytes with Deepgram. On success calls onTranscript with the text; on failure calls onError.
        /// </summary>
        public IEnumerator Transcribe(byte[] wavBytes, Action<string> onTranscript, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                onError?.Invoke("Deepgram API key is empty.");
                yield break;
            }

            if (wavBytes == null || wavBytes.Length < 44)
            {
                onError?.Invoke("Audio data too short for WAV.");
                yield break;
            }

            string query = $"?model={model}&language={language}&punctuate={(punctuate ? "true" : "false")}";
            string url = listenUrl + query;

            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(wavBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "audio/wav");
                req.SetRequestHeader("Authorization", "Token " + apiKey);

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"[STT] Deepgram request failed: {req.error} ({(int)req.responseCode})");
                    yield break;
                }

                string json = req.downloadHandler?.text;
                if (string.IsNullOrEmpty(json))
                {
                    onError?.Invoke("[STT] Empty response from Deepgram.");
                    yield break;
                }

                string transcript = ParseTranscript(json);
                if (string.IsNullOrEmpty(transcript))
                {
                    onError?.Invoke("[STT] No transcript in response. Raw: " + (json.Length > 200 ? json.Substring(0, 200) + "..." : json));
                    yield break;
                }

                onTranscript?.Invoke(transcript.Trim());
            }
        }

        static string ParseTranscript(string json)
        {
            try
            {
                var root = JsonUtility.FromJson<DeepgramResponse>(json);
                if (root?.results?.channels == null || root.results.channels.Length == 0)
                    return "";
                var alts = root.results.channels[0].alternatives;
                if (alts == null || alts.Length == 0)
                    return "";
                return alts[0].transcript ?? "";
            }
            catch (Exception e)
            {
                Debug.LogWarning("[STT] Parse error: " + e.Message);
                return "";
            }
        }
    }
}
