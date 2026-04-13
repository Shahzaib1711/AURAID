using System.Collections.Generic;
using TMPro;
using UnityEngine;
using AURAID.UI;

namespace AURAID.Training
{
    /// <summary>Applies Arabic to Training UI text (same idea as MainMenuLocalization for Language/Mode/Scenario). Call when Training root is shown.</summary>
    public static class TrainingUILocalization
    {
        /// <summary>All Training UI strings: English -> Arabic (training slides, quiz, QnA, registration, report).</summary>
        static readonly Dictionary<string, string> EnToAr = new Dictionary<string, string>
        {
            { "Register", "سجل" },
            { "Previous", "السابق" },
            { "Next", "التالي" },
            { "Name", "الاسم" },
            { "Email", "البريد الإلكتروني" },
            { "Email ID", "البريد الإلكتروني" },
            { "Please Enter the Required Information", "يرجى إدخال المعلومات المطلوبة" },
            { "Hold to Speak", "اضغط للتحدث" },
            { "Ask Anything!!", "اسأل أي شيء!!" },
            { "Finish", "إنهاء" },
            { "FINISH", "إنهاء" },
            { "SAVE", "حفظ" },
            { "Save and Exit", "حفظ وخروج" },
            { "Re attempt", "محاولة أخرى" },
            { "Re Attempt", "محاولة أخرى" },
            { "Confidence Before: 0/5", "الثقة قبل: 0/5" },
            { "Confidence After: 0/5", "الثقة بعد: 0/5" },
            { "Confidence Before:", "الثقة قبل:" },
            { "Confidence After:", "الثقة بعد:" },
            { "Compressions and Breathing Ratio", "نسبة الضغطات والتنفس" },
            { "Give 30 chest compressions", "أعطِ 30 ضغطة على الصدر" },
            { "If trained, give 2 rescue breaths", "إن كنت مدرباً، أعطِ نفسين إنقاذيين" },
            { "If not trained, continue hands-only CPR", "إن لم تكن مدرباً، واصِل الإنعاش باليدين فقط" },
            { "What is CPR?", "ما هو الإنعاش القلبي الرئوي؟" },
            { "How to do CPR", "كيفية عمل الإنعاش القلبي الرئوي" },
            { "The ABCD Approach", "نهج ABCD" },
            { "When to start CPR", "متى تبدأ الإنعاش" },
            { "When to stop CPR", "متى تتوقف عن الإنعاش" },
            { "How to perform Chest Compressions", "كيفية تنفيذ ضغطات الصدر" },
            { "Place two hands in the center of the chest", "ضع يديك في منتصف الصدر" },
            { "Place your hands in the center of the chest.", "ضع يديك في منتصف الصدر." },
            { "Start chest compressions.", "ابدأ ضغطات الصدر." },
            { "Push hard and fast", "اضغط بقوة وبسرعة" },
            { "Depth: about 5–6 cm (2 inches)", "العمق: حوالي 5–6 سم (2 بوصة)" },
            { "Rate: 100–120 compressions per minute", "المعدل: 100–120 ضغطة في الدقيقة" },
            { "Check for normal breathing (not gasping).", "تحقق من التنفس الطبيعي (وليس اللهاث)." },
            { "Make sure the airway is clear.", "تأكد من أن المجرى الهوائي مفتوح." },
            { "Use an AED if available.", "استخدم مزيل الرجفان إن وُجد." },
            { "Start CPR if: \nThey are not breathing normally", "ابدأ الإنعاش إذا:\nلا يتنفسون بشكل طبيعي" },
            { "Start CPR if: \nThey are only gasping", "ابدأ الإنعاش إذا:\nهم يلهثون فقط" },
            { "Start CPR if:", "ابدأ الإنعاش إذا:" },
            { "They are not breathing normally", "لا يتنفسون بشكل طبيعي" },
            { "They are only gasping", "هم يلهثون فقط" },
            { "Stop CPR only if: The scene becomes unsafe", "توقف عن الإنعاش فقط إذا: أصبح المشهد غير آمن" },
            { "Stop CPR only if: You are physically exhausted", "توقف عن الإنعاش فقط إذا: أنهكك التعب جسدياً" },
            { "Stop CPR only if: Professional medical help arrives", "توقف عن الإنعاش فقط إذا: وصلت المساعدة الطبية" },
            { "Stop CPR only if: The person starts breathing normally", "توقف عن الإنعاش فقط إذا: بدأ الشخص يتنفس طبيعياً" },
            { "It is used when someone's heart stops beating", "يُستخدم عندما يتوقف قلب الشخص عن النبض" },
            { "It can double or triple survival chances", "يمكن أن يضاعف فرص النجاة مرتين أو ثلاثاً" },
            { "It helps keep blood flowing to the brain", "يساعد على استمرار تدفق الدم إلى الدماغ" },
            { "Stands For: Cardiopulmonary Resuscitation", "الاختصار: الإنعاش القلبي الرئوي" },
            { "Begin compressions.", "ابدأ الضغط على الصدر." },
            { "Session complete.", "انتهت الجلسة." },
            { "Training Report", "تقرير التدريب" },
            { "Compressions: 0", "الضغطات: 0" },
            { "Avg Rate: 0 / min", "متوسط المعدل: 0 / دقيقة" },
            { "Avg Depth: 0.0 cm", "متوسط العمق: 0.0 سم" },
            { "Accuracy: 0%", "الدقة: 0%" },
            { "Correct Zone: 0%", "المنطقة الصحيحة: 0%" },
            { "Improvement: +0", "التحسن: +0" },
            { "Quiz Score: 0/9", "درجة الاختبار: 0/9" },
            { "Feedback: Maintain 100–120/min and keep depth between 5–6 cm.", "التغذية الراجعة: حافظ على 100–120/دقيقة والعمق بين 5–6 سم." },
            { "Session: AURAID-SESS-XXXX", "الجلسة: AURAID-SESS-XXXX" },
            { "On a scale of 1–5, how confident do you feel performing CPR after", "على مقياس 1–5، كم درجة ثقتك بأداء الإنعاش بعد" },
            { "What is the correct emergency number to call in Dubai for an ambulance?", "ما الرقم الصحيح للطوارئ لطلب إسعاف في دبي؟" },
            { "How long should you continue CPR?", "كم مدة استمرار الإنعاش؟" },
            { "What is the first thing you should do if someone collapses and is not responding?", "ما أول شيء تفعله إذا سقط شخص ولا يستجيب؟" },
            { "Where exactly should you place your hands during CPR?", "أين بالضبط تضع يديك أثناء الإنعاش؟" },
            { "How fast should chest compressions be given during CPR?", "ما سرعة ضغطات الصدر أثناء الإنعاش؟" },
            { "What is the compression-to-breath ratio for adult CPR?", "ما نسبة الضغطات إلى الأنفاس للبالغين؟" },
            { "If you are not trained in giving breaths, what should you do?", "إن لم تكن مدرباً على إعطاء الأنفاس، ماذا تفعل؟" },
            { "How deep should you press during chest compressions on an adult?", "ما عمق الضغط أثناء ضغطات الصدر للبالغ؟" },
            { "Should you stop CPR if you hear a cracking sound from the ribs?", "هل تتوقف عن الإنعاش إذا سمعت صوت تكسّر من الأضلاع؟" },
            { "A) 911", "أ) 911" },
            { "B) 112", "ب) 112" },
            { "C) 999", "ج) 999" },
            { "D) 998", "د) 998" },
            { "A) For 1 minute", "أ) لمدة دقيقة" },
            { "B) Until you get tired", "ب) حتى تتعب" },
            { "C) Until professional help arrives or the person breathes", "ج) حتى تصل المساعدة أو يتنفس الشخص" },
            { "D) For exactly 5 cycles", "د) لخمس دورات بالضبط" },
            { "A) Start chest compressions immediately", "أ) ابدأ ضغطات الصدر فوراً" },
            { "B) Check scene safety and responsiveness", "ب) تحقق من أمان المشهد والاستجابة" },
            { "C) Check pulse for 1 minute", "ج) تحقق من النبض لمدة دقيقة" },
            { "D) Shake them hard", "د) هزهم بقوة" },
            { "A) On the left side of the chest", "أ) على الجانب الأيسر من الصدر" },
            { "B) On the upper ribs", "ب) على الأضلاع العليا" },
            { "C) Center of the chest, lower half of sternum", "ج) منتصف الصدر، النصف السفلي من القص" },
            { "D) On the stomach", "د) على البطن" },
            { "A) 15:2", "أ) 15:2" },
            { "B) 30:2", "ب) 30:2" },
            { "C) 20:5", "ج) 20:5" },
            { "D) 10:1", "د) 10:1" },
            { "A) 1–2 cm", "أ) 1–2 سم" },
            { "B) 3–4 cm", "ب) 3–4 سم" },
            { "C) 5–6 cm", "ج) 5–6 سم" },
            { "D) 8–10 cm", "د) 8–10 سم" },
            { "A) 60–80 compressions per minute", "أ) 60–80 ضغطة في الدقيقة" },
            { "B) 80–100 compressions per minute", "ب) 80–100 ضغطة في الدقيقة" },
            { "C) 100–120 compressions per minute", "ج) 100–120 ضغطة في الدقيقة" },
            { "D) 140–160 compressions per minute", "د) 140–160 ضغطة في الدقيقة" },
            { "A) Stop CPR", "أ) توقف عن الإنعاش" },
            { "A) Stop CPR immediately", "أ) توقف عن الإنعاش فوراً" },
            { "B) Continue compressions", "ب) واصِل الضغطات" },
            { "B) Only give rescue breaths", "ب) أعطِ أنفاساً إنقاذية فقط" },
            { "B) Give 2 rescue breaths", "ب) أعطِ نفسين إنقاذيين" },
            { "C) Perform hands-only CPR", "ج) نفّذ إنعاشاً باليدين فقط" },
            { "D) Wait for ambulance", "د) انتظر الإسعاف" },
            { "D) Switch to only rescue breaths", "د) انتقل إلى الأنفاس الإنقاذية فقط" },
            { "A) Do nothing", "أ) لا تفعل شيئاً" },
            { "C) Reduce pressure completely", "ج) قلّل الضغط تماماً" },
            { "C) Give water", "ج) أعطِ ماءً" },
            { "D) Sit the person upright", "د) اجلس الشخص منتصباً" },
            // Hyphen variants (prefab may use '-' instead of en-dash)
            { "Depth: about 5-6 cm (2 inches)", "العمق: حوالي 5–6 سم (2 بوصة)" },
            { "Rate: 100-120 compressions per minute", "المعدل: 100–120 ضغطة في الدقيقة" },
            { "Feedback: Maintain 100-120/min and keep depth between 5-6 cm.", "التغذية الراجعة: حافظ على 100–120/دقيقة والعمق بين 5–6 سم." },
            { "On a scale of 1-5, how confident do you feel performing CPR after", "على مقياس 1–5، كم درجة ثقتك بأداء الإنعاش بعد" },
            { "A) 1-2 cm", "أ) 1–2 سم" },
            { "B) 3-4 cm", "ب) 3–4 سم" },
            { "C) 5-6 cm", "ج) 5–6 سم" },
            { "D) 8-10 cm", "د) 8–10 سم" },
            { "A) 60-80 compressions per minute", "أ) 60–80 ضغطة في الدقيقة" },
            { "B) 80-100 compressions per minute", "ب) 80–100 ضغطة في الدقيقة" },
            { "C) 100-120 compressions per minute", "ج) 100–120 ضغطة في الدقيقة" },
            { "D) 140-160 compressions per minute", "د) 140–160 ضغطة في الدقيقة" },
            // Prefab variants (right single quote U+2019, or no newline)
            { "It is used when someone\u2019s heart stops beating", "يُستخدم عندما يتوقف قلب الشخص عن النبض" },
            { "Start CPR if: They are only gasping", "ابدأ الإنعاش إذا:\nهم يلهثون فقط" },
            // Section / screen headings (training, quiz, QnA, report, registration)
            { "Quiz", "الاختبار" },
            { "Practical", "التطبيق العملي" },
            { "Hands-On", "التطبيق العملي" },
            { "Registration", "التسجيل" },
            { "Report", "التقرير" },
            { "Training", "التدريب" },
            { "Q&A", "أسئلة وأجوبة" },
            { "Ask a Question", "اطرح سؤالاً" },
            { "Question 1", "السؤال 1" },
            { "Question 2", "السؤال 2" },
            { "Question 3", "السؤال 3" },
            { "Question 4", "السؤال 4" },
            { "Question 5", "السؤال 5" },
            { "Question 6", "السؤال 6" },
            { "Question 7", "السؤال 7" },
            { "Question 8", "السؤال 8" },
            { "Question 9", "السؤال 9" },
            { "Confidence", "الثقة" },
            { "Registration complete.", "تم التسجيل." },
            { "Score", "الدرجة" },
            { "Your Score", "درجتك" },
            { "Correct", "صحيح" },
            { "Wrong", "خطأ" },
            { "Next Question", "السؤال التالي" },
            { "Previous Question", "السؤال السابق" },
        };

        /// <summary>Apply Arabic (or leave English) to all TMP_Text under root. Uses MainMenuLocalization fonts when available.</summary>
        public static void Apply(Transform root, bool useArabic)
        {
            if (root == null) return;

            TMP_FontAsset arabicFont = MainMenuLocalization.ArabicFont
                ?? Resources.Load<TMP_FontAsset>("Fonts/NotoSansArabic SDF")
                ?? Resources.Load<TMP_FontAsset>("NotoSansArabic SDF")
                ?? Resources.Load<TMP_FontAsset>("ArabicFont");
            TMP_FontAsset defaultFont = MainMenuLocalization.DefaultFont;

            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in texts)
            {
                if (tmp == null || string.IsNullOrEmpty(tmp.text)) continue;
                string trimmed = tmp.text.Trim();
                string ar = null;
                if (EnToAr.TryGetValue(trimmed, out ar)) { }
                else if (EnToAr.TryGetValue(trimmed.Replace("\n", " ").Replace("\r", " ").Trim(), out ar)) { }
                else if (EnToAr.TryGetValue(trimmed.Replace('\u2013', '-'), out ar)) { }
                else continue;

                if (useArabic && arabicFont != null)
                {
                    tmp.font = arabicFont;
                    tmp.isRightToLeftText = true;
                    tmp.text = ArabicShaping.Shape(ar);
                    tmp.fontStyle &= ~(FontStyles.Underline | FontStyles.Strikethrough);
                    tmp.ForceMeshUpdate(true, true);
                }
                else if (!useArabic && defaultFont != null)
                {
                    tmp.font = defaultFont;
                    tmp.isRightToLeftText = false;
                    tmp.text = trimmed;
                }
            }

            // Also match partial / common button labels (e.g. "Prev" as Previous)
            if (useArabic && arabicFont != null)
            {
                foreach (var tmp in texts)
                {
                    if (tmp == null || tmp.text == null) continue;
                    string t = tmp.text.Trim();
                    if (t == "Prev") { tmp.font = arabicFont; tmp.isRightToLeftText = true; tmp.text = ArabicShaping.Shape("السابق"); tmp.ForceMeshUpdate(true, true); }
                }
            }
        }
    }
}
