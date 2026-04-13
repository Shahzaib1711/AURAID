using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AURAID.Training
{
    /// <summary>
    /// Attach to each quiz question panel (Question1, Question2, ...). Wires A/B/C/D to QuizManager and shows
    /// right/wrong feedback: green if correct, red if wrong, and highlights the correct option when wrong.
    /// </summary>
    public class QuizQuestionCard : MonoBehaviour
    {
        [Tooltip("0 = first question, 1 = second, etc. Must match this panel's order in QuizScreens.")]
        [SerializeField] int questionIndex;

        [Header("Report PDF — leave empty to auto-find Heading* / Question* and label TMP under each option button")]
        [SerializeField] TMP_Text questionTextMesh;
        [SerializeField] TMP_Text optionTextA;
        [SerializeField] TMP_Text optionTextB;
        [SerializeField] TMP_Text optionTextC;
        [SerializeField] TMP_Text optionTextD;

        [Header("Option buttons (assign or leave empty to find by name A, B, C, D)")]
        [SerializeField] Button buttonA;
        [SerializeField] Button buttonB;
        [SerializeField] Button buttonC;
        [SerializeField] Button buttonD;

        [Header("Right / wrong feedback")]
        [Tooltip("Color when the selected option is correct.")]
        [SerializeField] Color correctColor = new Color(0.4f, 0.9f, 0.4f, 1f);
        [Tooltip("Color when the selected option is wrong (your choice).")]
        [SerializeField] Color wrongColor = new Color(1f, 0.4f, 0.35f, 1f);
        [Tooltip("Color for the correct option when user picked wrong (shows which was right).")]
        [SerializeField] Color correctAnswerRevealColor = new Color(0.5f, 1f, 0.5f, 1f);
        [Tooltip("Color for options that are not selected and not the correct answer.")]
        [SerializeField] Color normalColor = Color.white;

        Button[] _buttons;

        void Awake()
        {
            if (buttonA == null) buttonA = FindButton("A");
            if (buttonB == null) buttonB = FindButton("B");
            if (buttonC == null) buttonC = FindButton("C");
            if (buttonD == null) buttonD = FindButton("D");

            _buttons = new[] { buttonA, buttonB, buttonC, buttonD };

            ResolveReportTextReferencesIfEmpty();

            if (buttonA != null) buttonA.onClick.AddListener(() => Record(0));
            if (buttonB != null) buttonB.onClick.AddListener(() => Record(1));
            if (buttonC != null) buttonC.onClick.AddListener(() => Record(2));
            if (buttonD != null) buttonD.onClick.AddListener(() => Record(3));
        }

        /// <summary>
        /// Fills Question / A–D TMP refs when left empty, using the same layout as TrainingRoot (Heading*, option buttons with child Text TMP).
        /// You can still assign fields manually in the Inspector to override.
        /// </summary>
        void ResolveReportTextReferencesIfEmpty()
        {
            if (questionTextMesh == null)
                questionTextMesh = FindQuestionHeadingTmp();
            if (optionTextA == null && buttonA != null)
                optionTextA = FindOptionLabelTmp(buttonA);
            if (optionTextB == null && buttonB != null)
                optionTextB = FindOptionLabelTmp(buttonB);
            if (optionTextC == null && buttonC != null)
                optionTextC = FindOptionLabelTmp(buttonC);
            if (optionTextD == null && buttonD != null)
                optionTextD = FindOptionLabelTmp(buttonD);
        }

        static int TrimmedTextLength(string s)
        {
            return string.IsNullOrEmpty(s) ? 0 : s.Trim().Length;
        }

        /// <summary>
        /// If a button has multiple TMP children, Unity's first hit can be a short placeholder; prefer the longest non-empty label.
        /// </summary>
        static TMP_Text FindOptionLabelTmp(Button btn)
        {
            if (btn == null) return null;
            var texts = btn.GetComponentsInChildren<TMP_Text>(true);
            if (texts == null || texts.Length == 0) return null;
            TMP_Text best = texts[0];
            int bestLen = TrimmedTextLength(best != null ? best.text : null);
            for (int i = 1; i < texts.Length; i++)
            {
                int len = TrimmedTextLength(texts[i] != null ? texts[i].text : null);
                if (len > bestLen)
                {
                    bestLen = len;
                    best = texts[i];
                }
            }
            return best;
        }

        bool IsUnderAnyOptionButton(Transform t)
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                var b = _buttons[i];
                if (b == null) continue;
                if (t == b.transform || t.IsChildOf(b.transform))
                    return true;
            }
            return false;
        }

        TMP_Text FindQuestionHeadingTmp()
        {
            var tmps = GetComponentsInChildren<TMP_Text>(true);
            string preferredName = "Heading" + (questionIndex + 1);
            TMP_Text preferredHit = null;
            TMP_Text bestHeading = null;
            int bestHeadingLen = -1;
            for (int i = 0; i < tmps.Length; i++)
            {
                var tmp = tmps[i];
                if (tmp == null || IsUnderAnyOptionButton(tmp.transform)) continue;
                string n = tmp.gameObject.name;
                if (n.Equals(preferredName, StringComparison.OrdinalIgnoreCase))
                    preferredHit = tmp;
                if (n.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
                {
                    int len = TrimmedTextLength(tmp.text);
                    if (len > bestHeadingLen)
                    {
                        bestHeadingLen = len;
                        bestHeading = tmp;
                    }
                }
            }
            if (preferredHit != null)
                return preferredHit;
            if (bestHeading != null)
                return bestHeading;
            TMP_Text match = null;
            for (int i = 0; i < tmps.Length; i++)
            {
                var tmp = tmps[i];
                if (tmp == null || IsUnderAnyOptionButton(tmp.transform)) continue;
                string n = tmp.gameObject.name;
                if (n.Equals("QuestionText", StringComparison.OrdinalIgnoreCase)
                    || n.Equals("Question", StringComparison.OrdinalIgnoreCase))
                    match = tmp;
            }
            return match;
        }

        void OnEnable()
        {
            RegisterQuestionContentForReport();
            RefreshSelectionVisual();
            StopAllCoroutines();
            StartCoroutine(RegisterQuestionContentAfterLayout());
        }

        IEnumerator RegisterQuestionContentAfterLayout()
        {
            yield return null;
            RegisterQuestionContentForReport();
        }

        void RegisterQuestionContentForReport()
        {
            if (QuizManager.Instance == null) return;
            var qSrc = questionTextMesh != null ? questionTextMesh : FindQuestionHeadingTmp();
            string q = qSrc != null ? qSrc.text : "";
            string a = OptionReportText(optionTextA, buttonA);
            string b = OptionReportText(optionTextB, buttonB);
            string c = OptionReportText(optionTextC, buttonC);
            string d = OptionReportText(optionTextD, buttonD);
            QuizManager.Instance.RegisterQuestionContent(questionIndex, q, a, b, c, d);
        }

        string OptionReportText(TMP_Text assigned, Button btn)
        {
            var t = assigned != null ? assigned : FindOptionLabelTmp(btn);
            return t != null ? t.text : "";
        }

        Button FindButton(string name)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b.gameObject.name == name)
                    return b;
            }
            return null;
        }

        void Record(int optionIndex)
        {
            if (QuizManager.Instance != null)
                QuizManager.Instance.RecordAnswer(questionIndex, optionIndex);
            RefreshSelectionVisual();
        }

        void RefreshSelectionVisual()
        {
            int selected = QuizManager.Instance != null ? QuizManager.Instance.GetSelection(questionIndex) : -1;
            int correct = QuizManager.Instance != null ? QuizManager.Instance.GetCorrectAnswer(questionIndex) : -1;

            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] == null) continue;
                var graphic = _buttons[i].targetGraphic ?? _buttons[i].GetComponent<Graphic>();
                if (graphic == null) continue;

                if (i == selected)
                    graphic.color = (selected == correct) ? correctColor : wrongColor;
                else if (i == correct && selected >= 0 && selected != correct)
                    graphic.color = correctAnswerRevealColor;
                else
                    graphic.color = normalColor;
            }
        }
    }
}
