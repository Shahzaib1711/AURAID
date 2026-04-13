using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Single place to turn UI/raw copy into the exact string + language code passed to TTS.
    /// Keeps prefetch and VoiceController.Speak in sync.
    /// </summary>
    public static class TrainingTtsText
    {
        public static (string text, string langCode) Prepare(string rawMessage)
        {
            if (rawMessage == null) rawMessage = string.Empty;
            var lang = UnityEngine.Object.FindObjectOfType<TrainingLanguageController>();
            string code = lang != null ? lang.ttsLanguage : "en";
            if (code != "en" && code != "ar")
                code = "en";
            string text = rawMessage;
            if (code == "ar")
                text = TrainingLocalization.ToArabic(rawMessage);
            return (text, code);
        }
    }
}
