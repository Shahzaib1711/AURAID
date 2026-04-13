using TMPro;
using UnityEngine;

namespace AURAID.UI
{
    /// <summary>English and Arabic strings for the main menu screens (Language, Mode, Scenario).</summary>
    public static class MainMenuLocalization
    {
        public enum Screen { Language, Mode, Scenario }

        /// <summary>When set, Arabic text uses this font (must include Arabic glyphs). When null, menu shows in English.</summary>
        public static TMP_FontAsset ArabicFont;
        /// <summary>When set, English text uses this font (e.g. LiberationSans SDF). Used when switching back from Arabic.</summary>
        public static TMP_FontAsset DefaultFont;

        static TMP_FontAsset _loadedArabicFallback;

        // Language screen
        const string LangTitleEn = "Select a Language";
        const string LangTitleAr = "اختر اللغة";
        const string LangBtnAEn = "English";
        const string LangBtnAAr = "English";
        const string LangBtnBEn = "Arabic";
        const string LangBtnBAr = "العربية";

        // Mode screen
        const string ModeTitleEn = "Select a Mode";
        const string ModeTitleAr = "اختر الوضع";
        const string ModeBtnAEn = "Emergency";
        const string ModeBtnAAr = "طوارئ";
        const string ModeBtnBEn = "Training";
        const string ModeBtnBAr = "تدريب";

        // Scenario screen
        const string ScenarioTitleEn = "Select a Scenario";
        const string ScenarioTitleAr = "اختر السيناريو";
        const string ScenarioBtnAEn = "CPR";
        const string ScenarioBtnAAr = "إنعاش قلبي رئوي";

        public static void Apply(UIScreenRefs screen, Screen screenType, bool useArabic)
        {
            if (screen == null) return;

            // If Arabic requested but no Arabic font available, show English to avoid missing-glyph warnings
            TMP_FontAsset fontToUse;
            if (useArabic)
            {
                fontToUse = ArabicFont;
                if (fontToUse == null)
                    fontToUse = _loadedArabicFallback ?? (_loadedArabicFallback = Resources.Load<TMP_FontAsset>("Fonts/NotoSansArabic SDF"));
                if (fontToUse == null)
                    fontToUse = _loadedArabicFallback ?? (_loadedArabicFallback = Resources.Load<TMP_FontAsset>("NotoSansArabic SDF"));
                if (fontToUse == null)
                    fontToUse = _loadedArabicFallback ?? (_loadedArabicFallback = Resources.Load<TMP_FontAsset>("ArabicFont"));
                if (fontToUse == null)
                {
                    useArabic = false;
                    fontToUse = DefaultFont;
                }
            }
            else
            {
                fontToUse = DefaultFont;
            }

            string title, labelA, labelB, labelC;
            switch (screenType)
            {
                case Screen.Language:
                    title = LangTitleEn;
                    labelA = LangBtnAEn;
                    labelB = LangBtnBAr;
                    labelC = "";
                    break;
                case Screen.Mode:
                    title = useArabic ? ModeTitleAr : ModeTitleEn;
                    labelA = useArabic ? ModeBtnAAr : ModeBtnAEn;
                    labelB = useArabic ? ModeBtnBAr : ModeBtnBEn;
                    labelC = "";
                    break;
                case Screen.Scenario:
                    title = useArabic ? ScenarioTitleAr : ScenarioTitleEn;
                    labelA = useArabic ? ScenarioBtnAAr : ScenarioBtnAEn;
                    labelB = "";
                    labelC = "";
                    break;
                default:
                    return;
            }

            void SetTextAndFont(TMP_Text tmp, string value)
            {
                if (tmp == null) return;
                bool valueIsArabic = ContainsArabic(value);
                if (useArabic || valueIsArabic)
                {
                    // Use Arabic font + shaped text (FE70-FEFF) so letters connect. Font must include FE70-FEFF and 005F (see ArabicFontSetup.md).
                    TMP_FontAsset arabicFont = fontToUse;
                    if (valueIsArabic && (arabicFont == null || !useArabic))
                        arabicFont = ArabicFont ?? _loadedArabicFallback ?? Resources.Load<TMP_FontAsset>("Fonts/NotoSansArabic SDF") ?? Resources.Load<TMP_FontAsset>("NotoSansArabic SDF") ?? Resources.Load<TMP_FontAsset>("ArabicFont");
                    if (arabicFont != null)
                    {
                        tmp.fontStyle &= ~(FontStyles.Underline | FontStyles.Strikethrough);
                        tmp.font = arabicFont;
                        tmp.isRightToLeftText = true;
                        tmp.text = ArabicShaping.Shape(value);
                        tmp.ForceMeshUpdate(true, true);
                        return;
                    }
                }
                if (DefaultFont != null)
                    tmp.font = DefaultFont;
                tmp.isRightToLeftText = false;
                tmp.text = value;
            }

            // Special-case Language screen buttons so they stay stable when toggling back and forth.
            if (screenType == Screen.Language)
            {
                // Title
                if (screen.title != null)
                    SetTextAndFont(screen.title, title);

                // English button: always plain English, LTR, default font.
                if (screen.labelA != null)
                {
                    if (DefaultFont != null)
                        screen.labelA.font = DefaultFont;
                    screen.labelA.isRightToLeftText = false;
                    screen.labelA.text = LangBtnAEn;
                }

                // Arabic button: always العربية in Arabic font (if available) with shaping.
                if (screen.labelB != null)
                {
                    TMP_FontAsset arabicFont = ArabicFont ?? _loadedArabicFallback
                        ?? Resources.Load<TMP_FontAsset>("Fonts/NotoSansArabic SDF")
                        ?? Resources.Load<TMP_FontAsset>("NotoSansArabic SDF")
                        ?? Resources.Load<TMP_FontAsset>("ArabicFont");

                    if (arabicFont != null)
                    {
                        screen.labelB.font = arabicFont;
                        screen.labelB.isRightToLeftText = true;
                        screen.labelB.text = ArabicShaping.Shape(LangBtnBAr);
                        screen.labelB.ForceMeshUpdate(true, true);
                    }
                    else
                    {
                        if (DefaultFont != null)
                            screen.labelB.font = DefaultFont;
                        screen.labelB.isRightToLeftText = false;
                        screen.labelB.text = LangBtnBAr;
                    }
                }

                // No labelC on Language screen currently; fall through.
            }
            else
            {
                // Update assigned refs for Mode / Scenario screens as usual.
                if (screen.title != null)
                    SetTextAndFont(screen.title, title);
                SetTextAndFont(screen.labelA, labelA);
                SetTextAndFont(screen.labelB, labelB);
                if (screen.labelC != null && !string.IsNullOrEmpty(labelC))
                    SetTextAndFont(screen.labelC, labelC);

                // Force button labels via the actual Button components, so we don't depend
                // on labelA / labelB being wired in the inspector or on previous text.
                if (screen.buttonA != null)
                {
                    var txtA = screen.buttonA.GetComponentInChildren<TMP_Text>(true);
                    SetTextAndFont(txtA, labelA);
                }
                if (screen.buttonB != null)
                {
                    var txtB = screen.buttonB.GetComponentInChildren<TMP_Text>(true);
                    SetTextAndFont(txtB, labelB);
                }
                if (screen.buttonC != null && !string.IsNullOrEmpty(labelC))
                {
                    var txtC = screen.buttonC.GetComponentInChildren<TMP_Text>(true);
                    SetTextAndFont(txtC, labelC);
                }
            }

            // Fallback: update button labels by matching English text (if Label A/B/C not assigned)
            string btnAEn = screenType == Screen.Mode ? ModeBtnAEn : (screenType == Screen.Scenario ? ScenarioBtnAEn : LangBtnAEn);
            string btnBEn = screenType == Screen.Mode ? ModeBtnBEn : (screenType == Screen.Scenario ? "" : LangBtnBEn);
            string btnCEn = "";
            foreach (var t in screen.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == null || string.IsNullOrEmpty(t.text)) continue;
                if (t == screen.title || t == screen.labelA || t == screen.labelB || t == screen.labelC) continue;
                string trimmed = t.text.Trim();
                if (trimmed == btnAEn) SetTextAndFont(t, labelA);
                else if (trimmed == btnBEn) SetTextAndFont(t, labelB);
                else if (!string.IsNullOrEmpty(btnCEn) && trimmed == btnCEn) SetTextAndFont(t, labelC);
            }

            // Language screen: only screen.title gets the title; any other TMP_Text that looks like title or reversed → labelB (العربية)
            if (screenType == Screen.Language)
            {
                foreach (var t in screen.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t == null || string.IsNullOrEmpty(t.text)) continue;
                    string trimmed = t.text.Trim().Replace("\r", "").Replace("\n", " ");
                    bool isTitleOrReversed = trimmed == LangTitleEn
                        || trimmed == "a tceleS egaugnaL" || trimmed == "egaugnaL a tceleS"
                        || trimmed == "a tceleS egaugnal" || trimmed == "egaugnal a tceleS"
                        || (trimmed.Contains("a tceleS") && trimmed.Contains("egaugnal"));
                    if (!isTitleOrReversed) continue;
                    if (t == screen.title)
                        SetTextAndFont(t, title);
                    else
                        SetTextAndFont(t, labelB);
                }
            }

            // Fix: RTL must only be true when text is Arabic or shaped Arabic (FE70-FEFF); clear RTL on plain English so "Select a Language" doesn't show reversed
            foreach (var t in screen.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == null) continue;
                if (t.isRightToLeftText && !ContainsArabicOrShaped(t.text))
                    t.isRightToLeftText = false;
            }

            // Ensure any stray title-like text that wasn't caught above is set to labelB (Arabic button) so we never show reversed English
            if (screenType == Screen.Language)
            {
                foreach (var t in screen.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t == null || t == screen.title || t == screen.labelA) continue;
                    string trimmed = t.text.Trim().Replace("\r", "").Replace("\n", " ");
                    if (trimmed.Contains("a tceleS") || trimmed.Contains("egaugnal") || trimmed == LangTitleEn)
                        SetTextAndFont(t, labelB);
                }
            }

            // When showing English: overwrite stray Arabic with the title; never overwrite Language screen buttons.
            if (!useArabic)
            {
                foreach (var t in screen.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t == null || string.IsNullOrEmpty(t.text)) continue;
                    if (t == screen.labelA || t == screen.labelB) continue;
                    if (screenType == Screen.Language)
                    {
                        if (t.text.Trim() == LangBtnAEn) continue;
                        if (ContainsArabicOrShaped(t.text)) continue;
                    }
                    if (ContainsArabic(t.text))
                    {
                        if (fontToUse != null) t.font = fontToUse;
                        t.isRightToLeftText = false;
                        t.text = title;
                    }
                }
            }
        }

        static bool ContainsArabic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
                if (c >= '\u0600' && c <= '\u06FF') return true;
            return false;
        }

        /// <summary>True if text contains Arabic (0600–06FF) or presentation forms (FE70–FEFF). Used so we don't clear RTL on shaped Arabic.</summary>
        static bool ContainsArabicOrShaped(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
                if ((c >= '\u0600' && c <= '\u06FF') || (c >= '\uFE70' && c <= '\uFEFF')) return true;
            return false;
        }
    }
}
