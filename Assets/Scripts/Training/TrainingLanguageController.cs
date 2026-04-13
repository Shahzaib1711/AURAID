using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Holds the selected language for Training mode (same role as EmergencyController.ttsLanguage).
    /// UIFlowManager sets this when user selects Training + CPR. VoiceController and QnAManager read it for TTS.
    /// Attach to TrainingRoot.
    /// </summary>
    public class TrainingLanguageController : MonoBehaviour
    {
        [Tooltip("TTS language: 'en' or 'ar'. Set by UIFlowManager when entering Training; used by VoiceController and QnAManager.")]
        public string ttsLanguage = "en";

        public bool IsArabic => ttsLanguage == "ar";
    }
}
