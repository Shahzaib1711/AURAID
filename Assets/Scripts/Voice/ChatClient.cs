using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace AURAID.Voice
{
    /// <summary>
    /// Minimal OpenAI Chat Completions client for CPR training QnA.
    /// Use Ask(question, lang, onAnswer, onErr) as a coroutine.
    /// </summary>
    public class ChatClient : MonoBehaviour
    {
        [Header("OpenAI Chat")]
        public string chatUrl = "https://api.openai.com/v1/chat/completions";
        [Tooltip("DO NOT ship this inside the APK for production. Use a backend.")]
        public string apiKey = "";
        [Tooltip("Chat model, e.g. gpt-4o-mini or gpt-4.1-mini.")]
        public string model = "gpt-4o-mini";

        [Header("Generation")]
        [Range(0f, 2f)]
        public float temperature = 0.4f;
        [Tooltip("Max tokens for the answer (keep it short for VR subtitles).")]
        public int maxTokens = 120;

        [Serializable]
        class ChatMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        class ChatRequest
        {
            public string model;
            public ChatMessage[] messages;
            public float temperature;
            public int max_tokens;
        }

        [Serializable]
        class ChatResponse
        {
            public ChatChoice[] choices;
        }

        [Serializable]
        class ChatChoice
        {
            public ChatMessage message;
        }

        /// <summary>
        /// Ask the LLM for a CPR training answer.
        /// lang: "en" or "ar" (answers in that language).
        /// </summary>
        public IEnumerator Ask(string question, string lang, Action<string> onAnswer, Action<string> onErr)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                onErr?.Invoke("OpenAI Chat API key is empty.");
                yield break;
            }

            string locale = string.IsNullOrEmpty(lang) ? "en" : lang;
            bool isArabic = locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

            string systemPrompt = isArabic
                ? "You are a CPR training assistant. Answer briefly (1-2 short sentences) in Modern Standard Arabic. Focus only on CPR training guidance, not on diagnosis."
                : "You are a CPR training assistant. Answer briefly (1-2 short sentences) in clear English. Focus only on CPR training guidance, not on diagnosis.";

            var req = new ChatRequest
            {
                model = string.IsNullOrEmpty(model) ? "gpt-4o-mini" : model,
                temperature = temperature,
                max_tokens = Mathf.Max(16, maxTokens),
                messages = new[]
                {
                    new ChatMessage { role = "system", content = systemPrompt },
                    new ChatMessage { role = "user", content = question }
                }
            };

            string json = JsonUtility.ToJson(req);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

            using (var www = new UnityWebRequest(chatUrl, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    long code = www.responseCode;
                    string body = www.downloadHandler != null ? www.downloadHandler.text : "";
                    onErr?.Invoke($"[Chat] HTTP {code}: {www.error}\n{body}");
                    yield break;
                }

                try
                {
                    string body = www.downloadHandler.text;
                    var resp = JsonUtility.FromJson<ChatResponse>(body);
                    if (resp?.choices != null && resp.choices.Length > 0 && resp.choices[0].message != null)
                    {
                        string answer = resp.choices[0].message.content;
                        if (!string.IsNullOrWhiteSpace(answer))
                        {
                            onAnswer?.Invoke(answer.Trim());
                            yield break;
                        }
                    }
                    onErr?.Invoke("[Chat] Empty or malformed response.");
                }
                catch (Exception e)
                {
                    onErr?.Invoke("[Chat] Parse failed: " + e.Message);
                }
            }
        }
    }
}

