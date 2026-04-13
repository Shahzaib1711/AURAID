namespace AURAID.Training
{
    /// <summary>Short TTS lines for the instruction / info phase (not the practical CPR panel).</summary>
    public static class TrainingScreensVoice
    {
        /// <summary>Played when training info opens after registration (and on reattempt when returning to slides).</summary>
        public static string PleaseReadInstructionsCarefully(bool arabic)
        {
            if (arabic)
                return "أمامك الآن عدة شاشات فيها معلومات مهمة عن الإنعاش القلبي الرئوي. خذ وقتك واقرأ كل شاشة بعناية قبل أن تنتقل للخطوة التالية.";
            return "These screens share important CPR information with you. Take your time and read each one carefully before you move on.";
        }
    }
}
