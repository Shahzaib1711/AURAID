using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AURAID.Training;
using AURAID.Emergency;

namespace AURAID.UI
{
    public enum AuraidLanguage { English, Arabic }
    public enum AuraidMode { Training, Emergency }
    public enum AuraidScenario { CPR }

    public class UIFlowManager : MonoBehaviour
    {
        [Header("Training Root (for Training + CPR)")]
        [Tooltip("Root GameObject that contains the Training UI (e.g. TrainingRoot). Will be activated when user selects Training + CPR.")]
        [SerializeField] private GameObject trainingRoot;
        [Tooltip("Flow controller on the TrainingRoot. Used to show the Registration panel.")]
        [SerializeField] private TrainingFlowManager trainingFlowManager;

        [Header("Emergency Root (for Emergency + CPR)")]
        [Tooltip("Root GameObject that contains the Emergency CPR UI / scene root. Will be activated when user selects Emergency + CPR.")]
        [SerializeField] private GameObject emergencyRoot;
        [Tooltip("Emergency CPR controller on the EmergencyRoot. Used to configure language, etc.")]
        [SerializeField] private EmergencyController emergencyController;

        [Header("Standby (first screen — tap anywhere to continue)")]
        [Tooltip("Your standby_Screen root (e.g. full-screen panel). Shown on app start; tap opens Language.")]
        [SerializeField] GameObject standbyScreen;
        [Tooltip("Optional full-screen Button (stretch anchors). If empty, first Button under standbyScreen is used.")]
        [SerializeField] Button standbyTapButton;

        [Header("Screens (root objects under ONE Canvas)")]
        public UIScreenRefs screenLanguage;
        public UIScreenRefs screenMode;
        public UIScreenRefs screenScenario;

        [Header("Fonts for Arabic (fixes missing glyphs)")]
        [Tooltip("TMP Font Asset that includes Arabic. Create via Window > TextMeshPro > Font Asset Creator using e.g. Noto Sans Arabic.")]
        public TMP_FontAsset arabicFontForMenu;
        [Tooltip("Default TMP font for English (e.g. LiberationSans SDF). Used when switching back from Arabic.")]
        public TMP_FontAsset defaultFontForMenu;

        [Header("Current selection")]
        [SerializeField] private AuraidLanguage selectedLanguage = AuraidLanguage.English;
        [SerializeField] private AuraidMode selectedMode = AuraidMode.Emergency;
        [SerializeField] private AuraidScenario selectedScenario = AuraidScenario.CPR;

        [Header("Debug (log button clicks to verify interaction on device)")]
        [SerializeField] private bool logButtonClicks = true;

        bool _menuButtonsWired;
        bool _standbyButtonWired;

        void Awake()
        {
            WireButtons();

            // If Training Root is not assigned, the prefab can stay active and the Language screen stacks on top at Start().
            EnsureTrainingRootReference();

            // Ensure Training/Emergency UI is hidden at startup; it will be shown only after the appropriate selection.
            if (trainingRoot != null)
                trainingRoot.SetActive(false);
            if (emergencyRoot != null)
                emergencyRoot.SetActive(false);
        }

        void EnsureTrainingRootReference()
        {
            if (trainingRoot != null && trainingFlowManager != null)
                return;

            if (trainingFlowManager == null && trainingRoot != null)
                trainingFlowManager = trainingRoot.GetComponentInChildren<TrainingFlowManager>(true);

            if (trainingFlowManager == null)
                trainingFlowManager = FindObjectOfType<TrainingFlowManager>(true);

            if (trainingRoot == null && trainingFlowManager != null)
                trainingRoot = trainingFlowManager.gameObject;
        }

        void Start()
        {
            MainMenuLocalization.ArabicFont = arabicFontForMenu;
            MainMenuLocalization.DefaultFont = defaultFontForMenu;

            // Add Arabic font as fallback so menu TMP_Texts (e.g. Heading2) keep LiberationSans as primary
            // and get Arabic from fallback → no "Underline not available" warning from Arabic font
            if (arabicFontForMenu != null)
            {
                TMP_FontAsset fontToAddFallbackTo = defaultFontForMenu;
                if (fontToAddFallbackTo == null && screenMode != null && screenMode.title != null)
                    fontToAddFallbackTo = screenMode.title.font;
                if (fontToAddFallbackTo == null && screenLanguage != null && screenLanguage.title != null)
                    fontToAddFallbackTo = screenLanguage.title.font;
                if (fontToAddFallbackTo == null && screenMode != null)
                {
                    var t = screenMode.GetComponentInChildren<TMP_Text>(true);
                    if (t != null) fontToAddFallbackTo = t.font;
                }
                if (fontToAddFallbackTo != null && fontToAddFallbackTo.fallbackFontAssetTable != null &&
                    !fontToAddFallbackTo.fallbackFontAssetTable.Contains(arabicFontForMenu))
                {
                    fontToAddFallbackTo.fallbackFontAssetTable.Add(arabicFontForMenu);
                }
            }

            HideAll();
            ShowStandby();
            ValidateMenuWiring();
        }

        /// <summary>
        /// Logs missing Inspector wiring. Flow: Standby (tap) → Language (EN/AR) → Mode → Scenario (CPR).
        /// Only Training+CPR and Emergency+CPR start a session today.
        /// </summary>
        void ValidateMenuWiring()
        {
            if (standbyScreen == null)
                Debug.LogWarning("[UIFlowManager] Assign standbyScreen (first screen). Without it, call ShowLanguage() from code or user sees nothing.");

            if (screenLanguage == null)
                Debug.LogError("[UIFlowManager] Assign screenLanguage (UIScreenRefs on Language panel root).");
            else
            {
                if (screenLanguage.buttonA == null) Debug.LogError("[UIFlowManager] Language buttonA (English) is not assigned.");
                if (screenLanguage.buttonB == null) Debug.LogError("[UIFlowManager] Language buttonB (Arabic) is not assigned.");
            }

            if (screenMode == null)
                Debug.LogError("[UIFlowManager] Assign screenMode (UIScreenRefs on Mode panel root).");
            else
            {
                if (screenMode.buttonA == null) Debug.LogError("[UIFlowManager] Mode buttonA (Emergency) is not assigned.");
                if (screenMode.buttonB == null) Debug.LogError("[UIFlowManager] Mode buttonB (Training) is not assigned.");
                if (screenMode.previousButton == null)
                    Debug.LogWarning("[UIFlowManager] Mode previousButton is not assigned (back to Language).");
            }

            if (screenScenario == null)
                Debug.LogError("[UIFlowManager] Assign screenScenario (UIScreenRefs on Scenario panel root).");
            else
            {
                if (screenScenario.buttonA == null) Debug.LogError("[UIFlowManager] Scenario buttonA (CPR) is not assigned.");
                if (screenScenario.previousButton == null)
                    Debug.LogWarning("[UIFlowManager] Scenario previousButton is not assigned (back to Mode).");
            }
        }

        void WireButtons()
        {
            if (_menuButtonsWired)
                return;
            _menuButtonsWired = true;

            WireStandbyTap();

            ClearOnClick(screenLanguage?.buttonA);
            ClearOnClick(screenLanguage?.buttonB);
            ClearOnClick(screenMode?.buttonA);
            ClearOnClick(screenMode?.buttonB);
            ClearOnClick(screenMode?.previousButton);
            ClearOnClick(screenScenario?.buttonA);
            ClearOnClick(screenScenario?.buttonB);
            ClearOnClick(screenScenario?.previousButton);

            screenLanguage?.buttonA?.onClick.AddListener(() =>
            {
                if (logButtonClicks) Debug.Log("AURAID: Button English clicked.");
                selectedLanguage = AuraidLanguage.English;
                ShowMode();
            });

            screenLanguage?.buttonB?.onClick.AddListener(() =>
            {
                if (logButtonClicks) Debug.Log("AURAID: Button Arabic clicked.");
                selectedLanguage = AuraidLanguage.Arabic;
                ShowMode();
            });

            screenMode?.buttonA?.onClick.AddListener(() =>
            {
                if (logButtonClicks) Debug.Log("AURAID: Button Emergency clicked.");
                selectedMode = AuraidMode.Emergency;
                ShowScenario();
            });

            screenMode?.previousButton?.onClick.AddListener(() =>
            {
                if (logButtonClicks) Debug.Log("AURAID: Button Previous (Mode) clicked.");
                ShowLanguage();
            });

            screenMode?.buttonB?.onClick.AddListener(() =>
            {
                if (logButtonClicks) Debug.Log("AURAID: Button Training clicked.");
                selectedMode = AuraidMode.Training;
                ShowScenario();
            });

            screenScenario?.previousButton?.onClick.AddListener(() =>
            {
                if (logButtonClicks) Debug.Log("AURAID: Button Previous (Scenario) clicked.");
                ShowMode();
            });

            screenScenario?.buttonA?.onClick.AddListener(() =>
            {
                if (logButtonClicks) Debug.Log("AURAID: Button CPR clicked.");
                selectedScenario = AuraidScenario.CPR;
                OnScenarioConfirmed();
            });
        }

        void WireStandbyTap()
        {
            if (_standbyButtonWired || standbyScreen == null)
                return;

            Button b = standbyTapButton;
            if (b == null)
                b = standbyScreen.GetComponentInChildren<Button>(true);

            if (b == null)
            {
                Debug.LogWarning("[UIFlowManager] Standby screen has no Button. Add a full-screen transparent Button (raycast target) or assign standbyTapButton.");
                return;
            }

            standbyTapButton = b;
            ClearOnClick(standbyTapButton);
            standbyTapButton.onClick.AddListener(() =>
            {
                if (logButtonClicks) Debug.Log("AURAID: Standby screen tapped → Language.");
                ShowLanguage();
            });
            _standbyButtonWired = true;
        }

        static void ClearOnClick(Button b)
        {
            if (b != null)
                b.onClick.RemoveAllListeners();
        }

        void HideAll()
        {
            if (standbyScreen != null)
                standbyScreen.SetActive(false);
            screenLanguage?.SetActive(false);
            screenMode?.SetActive(false);
            screenScenario?.SetActive(false);
        }

        /// <summary>First screen after launch. Tap anywhere (wired Button) opens Language.</summary>
        public void ShowStandby()
        {
            screenLanguage?.SetActive(false);
            screenMode?.SetActive(false);
            screenScenario?.SetActive(false);
            if (standbyScreen != null)
                standbyScreen.SetActive(true);
            else
                ShowLanguage();
        }

        public void ShowLanguage()
        {
            if (standbyScreen != null)
                standbyScreen.SetActive(false);
            screenMode?.SetActive(false);
            screenScenario?.SetActive(false);
            screenLanguage?.SetActive(true);
            bool useArabic = (selectedLanguage == AuraidLanguage.Arabic);
            MainMenuLocalization.Apply(screenLanguage, MainMenuLocalization.Screen.Language, useArabic);
        }

        public void ShowMode()
        {
            if (standbyScreen != null)
                standbyScreen.SetActive(false);
            screenLanguage?.SetActive(false);
            screenScenario?.SetActive(false);
            screenMode?.SetActive(true);
            bool useArabic = (selectedLanguage == AuraidLanguage.Arabic);
            MainMenuLocalization.Apply(screenMode, MainMenuLocalization.Screen.Mode, useArabic);
        }

        /// <summary>Hide the Training UI root (used when navigating back from Registration to Scenario).</summary>
        public void HideTrainingRoot()
        {
            if (trainingRoot != null)
                trainingRoot.SetActive(false);
        }

        public void ShowScenario()
        {
            if (screenScenario == null && logButtonClicks)
                Debug.LogWarning("AURAID UIFlowManager: Screen Scenario is not assigned. Assign the Scenario panel (with UIScreenRefs) in the Inspector.");
            if (standbyScreen != null)
                standbyScreen.SetActive(false);
            screenLanguage?.SetActive(false);
            screenMode?.SetActive(false);
            screenScenario?.SetActive(true);
            if (screenScenario?.buttonB != null)
                screenScenario.buttonB.gameObject.SetActive(false);
            bool useArabic = (selectedLanguage == AuraidLanguage.Arabic);
            MainMenuLocalization.Apply(screenScenario, MainMenuLocalization.Screen.Scenario, useArabic);
        }

        /// <summary>Hide Emergency root and return to language selection.</summary>
        public void ReturnToLanguageFromEmergency()
        {
            if (logButtonClicks)
                Debug.Log("AURAID: Returning from Emergency to Language screen.");

            if (emergencyRoot != null)
                emergencyRoot.SetActive(false);

            ShowLanguage();
        }

        // Backward-compatible alias for existing serialized UnityEvent bindings.
        public void DevReturnToLanguageFromEmergency() => ReturnToLanguageFromEmergency();

        void OnScenarioConfirmed()
        {
            if (logButtonClicks)
                Debug.Log($"AURAID Selected → Language={selectedLanguage}, Mode={selectedMode}, Scenario={selectedScenario}");

            // If user chose Training + CPR, hide main menu screens and show the TrainingRoot + Registration panel.
            if (selectedMode == AuraidMode.Training && selectedScenario == AuraidScenario.CPR)
            {
                // Hide the three main menu screens so Training UI is not behind them.
                HideAll();

                if (trainingRoot != null)
                    trainingRoot.SetActive(true);

                // Set Training TTS language and apply Arabic to UI text (same as Emergency).
                bool useArabic = (selectedLanguage == AuraidLanguage.Arabic);
                var trainingLang = trainingRoot != null ? trainingRoot.GetComponentInChildren<Training.TrainingLanguageController>(true) : null;
                if (trainingLang != null)
                {
                    trainingLang.ttsLanguage = useArabic ? "ar" : "en";
                    if (logButtonClicks) Debug.Log($"AURAID: Training ttsLanguage={trainingLang.ttsLanguage}");
                }
                Training.TrainingUILocalization.Apply(trainingRoot != null ? trainingRoot.transform : null, useArabic);

                // Auto-wire TrainingFlowManager if not assigned.
                if (trainingFlowManager == null && trainingRoot != null)
                    trainingFlowManager = trainingRoot.GetComponentInChildren<TrainingFlowManager>(true);

                if (trainingFlowManager != null)
                {
                    if (logButtonClicks) Debug.Log("AURAID: Activating Training flow → ShowRegistration()");
                    trainingFlowManager.ShowRegistration();
                }
                else if (logButtonClicks)
                {
                    Debug.LogWarning("AURAID UIFlowManager: TrainingFlowManager is not assigned and could not be found under TrainingRoot. Assign it in the Inspector.");
                }

                return;
            }

            // If user chose Emergency + CPR, hide main menu screens and show the EmergencyRoot.
            if (selectedMode == AuraidMode.Emergency && selectedScenario == AuraidScenario.CPR)
            {
                // Hide the three main menu screens so Emergency UI is not behind them.
                HideAll();

                if (emergencyRoot != null)
                    emergencyRoot.SetActive(true);

                // Auto-wire EmergencyController if not assigned.
                if (emergencyController == null && emergencyRoot != null)
                    emergencyController = emergencyRoot.GetComponentInChildren<EmergencyController>(true);

                if (emergencyController != null)
                {
                    // Set TTS language based on selected language.
                    emergencyController.ttsLanguage = (selectedLanguage == AuraidLanguage.Arabic) ? "ar" : "en";

                    if (logButtonClicks)
                        Debug.Log($"AURAID: Activating Emergency CPR flow (ttsLanguage={emergencyController.ttsLanguage})");
                }
                else if (logButtonClicks)
                {
                    Debug.LogWarning("AURAID UIFlowManager: EmergencyController is not assigned and could not be found under EmergencyRoot. Assign it in the Inspector.");
                }

                return;
            }

            if (logButtonClicks)
                Debug.LogWarning(
                    $"[UIFlowManager] No session flow for Language={selectedLanguage}, Mode={selectedMode}, Scenario={selectedScenario}. " +
                    "Implemented: Training+CPR and Emergency+CPR only.");
        }
    }
}
