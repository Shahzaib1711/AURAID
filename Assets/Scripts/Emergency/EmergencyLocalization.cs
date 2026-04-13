using System.Collections.Generic;

namespace AURAID.Emergency
{
    /// <summary>Maps English CPR/emergency messages to Arabic for display and TTS when language is Arabic.</summary>
    public static class EmergencyLocalization
    {
        static readonly Dictionary<string, string> EnToAr = new Dictionary<string, string>
        {
            { "Start CPR now", "ابدأ الإنعاش القلبي الرئوي الآن" },
            { "Start CPR and prioritize rescue breaths early", "ابدأ الإنعاش وركز على التنفس الإنقاذي مبكراً" },
            { "Start CPR immediately", "ابدأ الإنعاش فوراً" },
            { "Keep steady compressions. Aim for 100 to 120 per minute.", "استمر بضغطات ثابتة. استهدف 100 إلى 120 ضغطة في الدقيقة." },
            { "Continue compressions now", "واصل الضغط على الصدر الآن" },
            { "Too slow", "بطيء جداً" },
            { "Too fast", "سريع جداً" },
            { "Push harder", "اضغط بقوة أكبر" },
            { "Press harder.", "اضغط بقوة أكبر." },
            { "Slow down. Aim for 100 to 120 compressions per minute.", "أبطئ. استهدف 100 إلى 120 ضغطة في الدقيقة." },
            { "Speed up. Aim for 100 to 120 compressions per minute.", "أسرع قليلاً. استهدف 100 إلى 120 ضغطة في الدقيقة." },
            { "Ease off slightly — still firm compressions.", "خفف القليل مع الإبقاء على ضغط ثابت." },
            { "Allow full recoil between compressions.", "اترك الصدر يرتفع بالكامل بين الضغطات." },
            { "Keep your arms straight, shoulders over hands.", "أبقِ ذراعيك مستقيمتين وكتفيك فوق يديك." },
            { "Adjust your posture. Straighten your arms — shoulders directly over your hands.", "اضبط وضعيتك. مدّ ذراعيك — الكتفان فوق اليدين مباشرة." },
            { "Adjust your posture. Position your shoulders over your hands.", "اضبط وضعيتك. ضع كتفيك فوق يديك." },
            { "Possible movement detected. Pause compressions and check if the person is breathing.", "يُشتبه بحركة. توقف عن الضغط وتحقق من التنفس." },
            { "Too hard. Reduce force slightly.", "قوي جداً. قلل القوة قليلاً." },
            { "Allow full chest recoil between compressions.", "اترك الصدر يرجع بالكامل بين الضغطات." },
            { "Allow full chest recoil", "اترك الصدر يرجع بالكامل" },
            { "Good compressions. Keep pace and depth.", "ضغط جيد. حافظ على السرعة والعمق." },
            { "Good compressions", "ضغط جيد" },
            { "Use two hands in the center of the chest. Depth 5–6 cm.", "استخدم يديك في منتصف الصدر. العمق 5–6 سم." },
            { "Use two fingers in the center of the chest. Depth ~4 cm.", "استخدم إصبعين في منتصف الصدر. العمق حوالي 4 سم." },
            { "Use one or two hands depending on size. Depth ~5 cm.", "استخدم يداً أو اثنتين حسب الحجم. العمق حوالي 5 سم." },
            { "Provide oxygen support", "وفر دعم الأكسجين" },
            { "Oxygen support", "دعم الأكسجين" },
            { "SpO₂ is critically low but breathing is present.", "الأكسجين منخفض جداً لكن هناك تنفس." },
            { "Low SpO₂ detected. Provide oxygen support and monitor.", "انخفاض الأكسجين. وفر الأكسجين وراقب." },
            { "Unresponsive but breathing detected. Monitor closely, keep airway clear, call for help.", "لا يستجيب لكن يتنفس. راقب، أبقِ المجرى مفتوحاً، استدعِ المساعدة." },
            { "Unresponsive with critically low heart activity. Start CPR now.", "لا يستجيب ونشاط القلب منخفض جداً. ابدأ الإنعاش الآن." },
            { "Monitoring vitals", "مراقبة العلامات الحيوية" },
            { "Monitoring vitals. Check responsiveness, breathing, pulse/HR, and SpO₂.", "مراقبة العلامات. تحقق من الاستجابة والتنفس والنبض والأكسجين." },
            { "Drowning suspected", "اشتباه غرق" },
            { "Pregnancy: perform left uterine displacement", "حمل: نفذ إزاحة الرحم لليسار" },
            { "Uncertain/borderline but life-threatening signs detected.", "علامات غير مؤكدة لكن مهددة للحياة." },
            { "Give rescue breaths early (hypoxia-first).", "أعطِ أنفاساً إنقاذية مبكراً." },
            { "Apply left uterine displacement.", "طبّق إزاحة الرحم لليسار." },
        };

        /// <summary>Returns Arabic translation for known English phrases; otherwise returns the original message.</summary>
        public static string ToArabic(string englishMessage)
        {
            if (string.IsNullOrEmpty(englishMessage)) return englishMessage;

            string msg = englishMessage.Trim();
            string bestAr = null;
            int bestLen = 0;
            foreach (var kv in EnToAr)
            {
                if (msg.Contains(kv.Key) && kv.Key.Length > bestLen)
                {
                    bestLen = kv.Key.Length;
                    bestAr = kv.Value;
                }
            }
            if (bestAr != null) return bestAr;

            if (msg.Contains("Increase to") && msg.Contains("/min"))
                return "بطيء جداً. زد السرعة إلى 100–120 ضغطة في الدقيقة.";
            if (msg.Contains("Slow to") && msg.Contains("/min"))
                return "سريع جداً. خفف السرعة إلى 100–120 ضغطة في الدقيقة.";

            return englishMessage;
        }
    }
}
