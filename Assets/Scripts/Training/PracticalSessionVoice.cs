using System;
using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Polite, personalized practical-CPR voice lines (English / Arabic). Uses first name when available.
    /// </summary>
    public static class PracticalSessionVoice
    {
        /// <summary>Matches TrainingLanguageController so phrasing matches TTS even before VoiceController.Start.</summary>
        public static bool IsArabicTrainingLanguage()
        {
            var lang = UnityEngine.Object.FindObjectOfType<TrainingLanguageController>();
            return lang != null && lang.ttsLanguage == "ar";
        }

        public static string FirstNameOrEmpty(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            var parts = fullName.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;
            var first = parts[0];
            if (first.Length == 1) return first.ToUpperInvariant();
            return char.ToUpperInvariant(first[0]) + first.Substring(1);
        }

        /// <summary>One combined opening for the practical panel: greet once, place hands, then begin compressions (no second greeting clip).</summary>
        public static string OpeningPracticalSession(string traineeFullName, bool arabic)
        {
            string first = FirstNameOrEmpty(traineeFullName);
            if (arabic)
            {
                if (string.IsNullOrEmpty(first))
                    return "مرحباً! عندما تكون جاهزاً، ضع يديك برفقاً في منتصف الصدر. عندما تشعر بالراحة، ابدأ ضغطات صدرية ثابتة — سأبقى معك وأقدّم لك نصائح لطيفة أثناء التمرين.";
                return $"مرحباً {first}! عندما تكون جاهزاً، ضع يديك برفقاً في منتصف الصدر. عندما تشعر بالراحة، ابدأ ضغطات صدرية ثابتة — سأبقى معك وأقدّم لك نصائح لطيفة أثناء التمرين.";
            }
            if (string.IsNullOrEmpty(first))
                return "Hello! When you're ready, place your hands gently in the center of the chest. When you feel comfortable, begin steady chest compressions — I'll stay with you and share gentle tips along the way.";
            return $"Hello {first}! When you're ready, place your hands gently in the center of the chest. When you feel comfortable, begin steady chest compressions — I'll stay with you and share gentle tips along the way.";
        }

        /// <summary>Played when the practical briefing / intro screen is shown (before the user presses Start).</summary>
        public static string BriefingScreenIntro(string traineeFullName, bool arabic)
        {
            string first = FirstNameOrEmpty(traineeFullName);
            if (arabic)
            {
                if (string.IsNullOrEmpty(first))
                    return "سننتقل الآن إلى الجزء العملي من التمرين. هذه الشاشة تعرض إيجازاً سريعاً عن جلسة الإنعاش القلبي الرئوي العملية. اقرأ المعلومات بعناية، ثم اضغط ابدأ عندما تكون مستعداً للمتابعة.";
                return $"{first}، سننتقل الآن إلى الجزء العملي من التمرين. هذه الشاشة تعرض إيجازاً سريعاً عن جلسة الإنعاش القلبي الرئوي العملية. اقرأ المعلومات بعناية، ثم اضغط ابدأ عندما تكون مستعداً للمتابعة.";
            }
            if (string.IsNullOrEmpty(first))
                return "The hands-on CPR practice will start next. This screen gives a short briefing about it. Please read the information carefully, then press Start when you're ready to continue.";
            return $"{first}, the hands-on CPR practice will start next. This screen gives a short briefing about it. Please read the information carefully, then press Start when you're ready to continue.";
        }

        /// <summary>Legacy single-step welcome (prefetch / tooling). Prefer <see cref="OpeningPracticalSession"/> for the practical CPR start.</summary>
        public static string WelcomePlaceHands(string traineeFullName, bool arabic)
        {
            string first = FirstNameOrEmpty(traineeFullName);
            if (arabic)
            {
                if (string.IsNullOrEmpty(first))
                    return "مرحباً! عندما تكون جاهزاً، ضع يديك برفقاً في منتصف الصدر.";
                return $"مرحباً {first}! عندما تكون جاهزاً، ضع يديك برفقاً في منتصف الصدر.";
            }
            if (string.IsNullOrEmpty(first))
                return "Hello! When you're ready, please place your hands gently in the center of the chest.";
            return $"Hello {first}! When you're ready, please place your hands gently in the center of the chest.";
        }

        public static string SessionComplete(string traineeFullName, bool arabic)
        {
            string first = FirstNameOrEmpty(traineeFullName);
            if (arabic)
            {
                if (string.IsNullOrEmpty(first))
                    return "أحسنت — انتهت جلستك التدريبية. شكراً لك على وقتك وجهدك.";
                return $"أحسنت يا {first} — انتهت جلستك التدريبية. شكراً لك على وقتك وجهدك في التمرين.";
            }
            if (string.IsNullOrEmpty(first))
                return "Wonderful work — your practice session is complete. Thank you for taking the time to train with us today.";
            return $"Wonderful work, {first} — your practice session is complete. Thank you for taking the time to practice with us today.";
        }

        public static string CoachingDepthTooLight(bool arabic)
        {
            if (arabic)
                return "نصيحة بسيطة: حاول الضغط بثبات أكثر قليلاً نحو خمسة إلى ستة سنتيمترات — أنت تبلي بلاءً حسناً.";
            return "A gentle suggestion: try pressing a little more firmly so we reach about five to six centimeters of depth — you're doing really well.";
        }

        public static string CoachingDepthTooHard(bool arabic)
        {
            if (arabic)
                return "لنخفّف الضغط قليلاً — نهدف إلى خمسة أو ستة سنتيمترات بثبات وراحة. أنت قريب جداً من المطلوب.";
            return "Let's ease the pressure slightly — we're aiming for a steady, comfortable five to six centimeters. You're very close.";
        }

        public static string CoachingRateTooSlow(bool arabic)
        {
            if (arabic)
                return "إذا أمكن، زِد الإيقاع قليلاً بثبات — نستهدف حوالي مئة إلى مئة وعشرين ضغطة في الدقيقة. يمكنك ذلك.";
            return "If you can, try a slightly quicker, steady rhythm — we're aiming for about one hundred to one hundred twenty compressions per minute. You've got this.";
        }

        public static string CoachingRateTooFast(bool arabic)
        {
            if (arabic)
                return "لنبطئ قليلاً — إيقاع ثابت بين مئة ومئة وعشرين ضغطة في الدقيقة مثالي. أنت تتقدم بشكل رائع.";
            return "Let's slow the pace just a touch — a steady rhythm between one hundred and one hundred twenty compressions per minute works best. You're doing great.";
        }

        public static string CoachingRecoil(bool arabic)
        {
            if (arabic)
                return "تذكّر أن تترك الصدر يرتفع بالكامل بين الضغطات — هذا يساعد القلب على الامتلاء. أحسنت.";
            return "Remember to let the chest rise fully between compressions — that helps the heart refill nicely. Well done keeping going.";
        }
    }
}
