using System.Collections.Generic;

namespace AURAID.Training
{
    /// <summary>Maps English Training/CPR messages to Arabic for TTS and display when language is Arabic (same idea as EmergencyLocalization).</summary>
    public static class TrainingLocalization
    {
        static readonly Dictionary<string, string> EnToAr = new Dictionary<string, string>
        {
            { "Place your hands in the center of the chest.", "ضع يديك في منتصف الصدر." },
            { "Begin compressions.", "ابدأ الضغط على الصدر." },
            { "Session complete.", "انتهت الجلسة." },
            { "Push harder. Compress 5 to 6 centimeters.", "اضغط بقوة أكبر. عمق الضغط 5 إلى 6 سنتيمترات." },
            { "Do not push too hard. Aim for 5 to 6 centimeters.", "لا تضغط بقوة زائدة. الهدف 5 إلى 6 سنتيمترات." },
            { "Compress a bit faster. Aim for 100 to 120 per minute.", "اضغط أسرع قليلاً. الهدف 100 إلى 120 ضغطة في الدقيقة." },
            { "Slow down slightly. Aim for 100 to 120 compressions per minute.", "أبطئ قليلاً. الهدف 100 إلى 120 ضغطة في الدقيقة." },
            { "Allow the chest to fully recoil between compressions.", "اترك الصدر يرجع بالكامل بين الضغطات." },
            // QnA bot replies
            { "Chest compressions should be 5 to 6 centimeters deep.", "عمق الضغط على الصدر يجب أن يكون 5 إلى 6 سنتيمترات." },
            { "Maintain a compression rate between 100 and 120 per minute.", "حافظ على معدل ضغط بين 100 و120 ضغطة في الدقيقة." },
            { "Continue CPR until emergency responders arrive.", "واصِل الإنعاش حتى وصول المسعفين." },
            { "Focus on maintaining correct compression depth and rhythm.", "ركز على الحفاظ على عمق الضغط والإيقاع الصحيحين." },
        };

        /// <summary>Returns Arabic translation for known English phrases; otherwise returns the original.</summary>
        public static string ToArabic(string englishMessage)
        {
            if (string.IsNullOrEmpty(englishMessage)) return englishMessage;
            string msg = englishMessage.Trim();
            return EnToAr.TryGetValue(msg, out string ar) ? ar : englishMessage;
        }
    }
}
