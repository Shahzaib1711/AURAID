using System.Collections.Generic;
using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Holds correct answers for the quiz, records user selections, and provides score.
    /// Attach to QuizRoot (or same object as QuizScreensController). Set correct answers in Inspector (0=A, 1=B, 2=C, 3=D).
    /// </summary>
    public class QuizManager : MonoBehaviour
    {
        public static QuizManager Instance { get; private set; }

        [Header("Correct answers (0=A, 1=B, 2=C, 3=D) for each question in order")]
        [Tooltip("One entry per quiz question. Index 0 = Question 1, etc.")]
        [SerializeField] int[] correctAnswers = new int[] { 0, 1, 2, 0, 1, 2, 0, 1, 2 };

        readonly List<int> _userSelections = new List<int>();
        string[] _questionTexts;
        string[][] _optionTexts;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>Call when entering the quiz (e.g. from QuizScreensController.ShowFirst).</summary>
        public void ResetQuiz()
        {
            _userSelections.Clear();
            int count = correctAnswers != null && correctAnswers.Length > 0 ? correctAnswers.Length : 9;
            for (int i = 0; i < count; i++)
                _userSelections.Add(-1);

            _questionTexts = new string[count];
            _optionTexts = new string[count][];
            for (int i = 0; i < count; i++)
                _optionTexts[i] = new string[4];
        }

        /// <summary>
        /// Called from each QuizQuestionCard (OnEnable) with UI text for the PDF report.
        /// </summary>
        public void RegisterQuestionContent(int index, string question, string optionA, string optionB, string optionC, string optionD)
        {
            if (_questionTexts == null || _optionTexts == null) return;
            if (index < 0 || index >= _questionTexts.Length) return;
            _questionTexts[index] = question != null ? question.Trim() : "";
            if (_optionTexts[index] == null) _optionTexts[index] = new string[4];
            _optionTexts[index][0] = optionA != null ? optionA.Trim() : "";
            _optionTexts[index][1] = optionB != null ? optionB.Trim() : "";
            _optionTexts[index][2] = optionC != null ? optionC.Trim() : "";
            _optionTexts[index][3] = optionD != null ? optionD.Trim() : "";
        }

        static string OptionLetter(int optionIndex)
        {
            if (optionIndex < 0 || optionIndex > 3) return "—";
            return ((char)('A' + optionIndex)).ToString();
        }

        /// <summary>
        /// TMP labels often already start with "A) " or "D)". We add the letter in the report, so strip one or more redundant prefixes.
        /// </summary>
        static string StripRedundantOptionPrefixes(string body, string letter)
        {
            if (string.IsNullOrEmpty(body) || letter.Length != 1) return body;
            char want = char.ToUpperInvariant(letter[0]);
            body = body.TrimStart();
            while (body.Length > 0)
            {
                if (char.ToUpperInvariant(body[0]) != want) break;
                int i = 1;
                while (i < body.Length && char.IsWhiteSpace(body[i])) i++;
                if (i >= body.Length) break;
                char sep = body[i];
                if (sep != ')' && sep != '.' && sep != ':' && sep != '-') break;
                i++;
                while (i < body.Length && char.IsWhiteSpace(body[i])) i++;
                body = i >= body.Length ? "" : body.Substring(i).TrimStart();
            }
            return body;
        }

        string FormatAnswerLine(int questionIndex, int optionIndex)
        {
            if (optionIndex < 0 || optionIndex > 3) return "No answer";
            string letter = OptionLetter(optionIndex);
            if (_optionTexts != null && questionIndex >= 0 && questionIndex < _optionTexts.Length
                && _optionTexts[questionIndex] != null
                && optionIndex < _optionTexts[questionIndex].Length)
            {
                string body = _optionTexts[questionIndex][optionIndex];
                if (!string.IsNullOrEmpty(body))
                {
                    body = StripRedundantOptionPrefixes(body, letter);
                    if (!string.IsNullOrEmpty(body))
                        return $"{letter}) {body}";
                }
            }
            return $"{letter})";
        }

        /// <summary>
        /// Rows for Firestore / PDF: question, user_answer, is_correct, and correct_answer when wrong.
        /// </summary>
        public List<Dictionary<string, object>> BuildQuizReportItems()
        {
            var list = new List<Dictionary<string, object>>();
            int total = correctAnswers != null ? correctAnswers.Length : 0;
            for (int i = 0; i < total; i++)
            {
                int sel = i < _userSelections.Count ? _userSelections[i] : -1;
                int corr = GetCorrectAnswer(i);
                bool ok = sel >= 0 && sel == corr;
                string qText = (_questionTexts != null && i < _questionTexts.Length && !string.IsNullOrEmpty(_questionTexts[i]))
                    ? _questionTexts[i]
                    : $"Question {i + 1}";

                var row = new Dictionary<string, object>
                {
                    { "question", qText },
                    { "user_answer", FormatAnswerLine(i, sel) },
                    { "is_correct", ok },
                };
                if (!ok)
                    row["correct_answer"] = FormatAnswerLine(i, corr);
                list.Add(row);
            }
            return list;
        }

        /// <summary>Call when user taps an option. questionIndex 0-based, optionIndex 0=A, 1=B, 2=C, 3=D.</summary>
        public void RecordAnswer(int questionIndex, int optionIndex)
        {
            if (questionIndex < 0 || questionIndex >= _userSelections.Count) return;
            if (optionIndex < 0 || optionIndex > 3) return;
            _userSelections[questionIndex] = optionIndex;
        }

        /// <summary>Returns (correct count, total questions).</summary>
        public (int correct, int total) GetScore()
        {
            int total = correctAnswers != null ? correctAnswers.Length : 0;
            int correct = 0;
            for (int i = 0; i < total && i < _userSelections.Count; i++)
            {
                if (_userSelections[i] == correctAnswers[i])
                    correct++;
            }
            return (correct, total);
        }

        /// <summary>Whether the user has selected an answer for this question.</summary>
        public bool HasAnswer(int questionIndex)
        {
            if (questionIndex < 0 || questionIndex >= _userSelections.Count) return false;
            return _userSelections[questionIndex] >= 0;
        }

        /// <summary>Returns the selected option index (0-3) or -1 if not answered.</summary>
        public int GetSelection(int questionIndex)
        {
            if (questionIndex < 0 || questionIndex >= _userSelections.Count) return -1;
            return _userSelections[questionIndex];
        }

        /// <summary>Returns the correct option index (0-3) for this question.</summary>
        public int GetCorrectAnswer(int questionIndex)
        {
            if (correctAnswers == null || questionIndex < 0 || questionIndex >= correctAnswers.Length) return -1;
            return correctAnswers[questionIndex];
        }
    }
}
