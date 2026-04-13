using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace AURAID.Voice
{
    public class TtsClient : MonoBehaviour
    {
        [Header("OpenAI TTS")]
        public string ttsUrl = "https://api.openai.com/v1/audio/speech";
        [Tooltip("DO NOT ship this inside the APK for production. Use a backend.")]
        public string apiKey = "";

        [Header("Voice settings")]
        public string model = "gpt-4o-mini-tts";
        public string voice = "alloy";
        [Tooltip("Must be \"wav\" to match WavUtility decoder. OpenAI uses response_format in JSON.")]
        public string responseFormat = "wav";

        [Header("Reliability")]
        [Tooltip("Max retries for 429 and 5xx.")]
        public int maxRetries = 5;
        public float initialBackoffSec = 1f;
        public float maxBackoffSec = 20f;

        [Serializable]
        class OpenAiTtsReq
        {
            public string model;
            public string voice;
            public string input;
            public string response_format;
            public string instructions;
        }

        /// <summary>
        /// Synthesize with exponential backoff + retry for 429 and 5xx.
        /// Uses Retry-After header when present. On success, invokes onAudioBytes with raw WAV bytes (decode in caller).
        /// </summary>
        public IEnumerator Synthesize(string text, string lang, Action<byte[]> onAudioBytes, Action<string> onErr)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                onErr?.Invoke("OpenAI API key is empty.");
                yield break;
            }

            string instructions = null;
            if (!string.IsNullOrEmpty(lang))
            {
                if (lang.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
                    instructions = "Speak in Arabic.";
                else
                    instructions = $"Speak in {lang}.";
            }

            // Request WAV so response matches WavUtility.FromWavBytes (never request mp3 here)
            var payload = new OpenAiTtsReq
            {
                model = model,
                voice = voice,
                input = text,
                response_format = "wav",
                instructions = instructions
            };

            string json = JsonUtility.ToJson(payload);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            float backoff = initialBackoffSec;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                using (var req = new UnityWebRequest(ttsUrl, "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        byte[] data = req.downloadHandler?.data;
                        if (data != null && data.Length > 0)
                        {
                            onAudioBytes?.Invoke(data);
                            yield break;
                        }
                        onErr?.Invoke("[TTS] Success but no audio data received.");
                        yield break;
                    }

                    long code = req.responseCode;
                    string responseBody = req.downloadHandler?.text ?? "";

                    bool isRateLimited = (code == 429);
                    bool isServerError = (code >= 500 && code <= 599);

                    if (isRateLimited)
                        Debug.LogWarning(OpenAi429Helper.Diagnose429(responseBody));

                    if ((isRateLimited || isServerError) && attempt < maxRetries)
                    {
                        float waitSec = backoff;
                        string retryAfter = req.GetResponseHeader("Retry-After");
                        if (!string.IsNullOrEmpty(retryAfter) && float.TryParse(retryAfter, out float ra))
                            waitSec = Mathf.Max(waitSec, ra);

                        Debug.LogWarning($"[TTS] HTTP {code}. Retrying in {waitSec:0.0}s (attempt {attempt + 1}/{maxRetries})");
                        yield return new WaitForSeconds(waitSec);

                        backoff = Mathf.Min(maxBackoffSec, backoff * 2f);
                        backoff += UnityEngine.Random.Range(0f, 0.25f);
                        continue;
                    }

                    onErr?.Invoke($"[TTS] Failed HTTP {code}. {req.error}\n{responseBody}");
                    yield break;
                }
            }

            onErr?.Invoke("[TTS] Failed: exceeded max retries.");
        }
    }
}
