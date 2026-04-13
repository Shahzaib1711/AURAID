using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using AURAID.UI;

namespace AURAID.Training
{
    /// <summary>
    /// Handles the Training registration UI and talks to TrainingManager and TrainingFlowManager.
    /// Attach this to the RegistrationCanvas root or a dedicated controller object.
    /// </summary>
    public class RegistrationHandler : MonoBehaviour
    {
        [Header("Registration Inputs")]
        [SerializeField] TMP_InputField nameInput;
        [SerializeField] TMP_InputField emailInput;

        [Header("Optional: initial confidence before training (0–5, same scale as report)")]
        [SerializeField] int defaultConfidenceBefore = 0;

        [Header("Flow (optional)")]
        [SerializeField] TrainingFlowManager flowManager;

        [Header("Status (optional UI)")]
        [SerializeField] TMP_Text statusLabel;

        bool _isSubmitting;

        void OnEnable()
        {
            // When the registration panel becomes visible, automatically focus the
            // name input field so the user can start typing immediately.
            if (nameInput != null)
            {
                nameInput.Select();
                nameInput.ActivateInputField();

                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null)
                    es.SetSelectedGameObject(nameInput.gameObject);
            }
        }

        /// <summary>
        /// Call from Previous button.
        /// In the old multi-scene setup this unloaded the TrainingMode scene.
        /// In the new single-scene setup, it should return to the Scenario selection screen.
        /// </summary>
        public void OnGoBack()
        {
            // Legacy behavior: if a separate TrainingMode scene is loaded, unload it.
            var scene = SceneManager.GetSceneByName("TrainingMode");
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync("TrainingMode");
                return;
            }

            // Current behavior: we're in a single scene; hide Training UI and go back to the Scenario screen.
            var uiFlow = FindObjectOfType<UIFlowManager>();
            if (uiFlow != null)
            {
                uiFlow.HideTrainingRoot();
                uiFlow.ShowScenario();
            }
            else
            {
                Debug.LogWarning("AURAID RegistrationHandler: UIFlowManager not found; cannot navigate back to Scenario screen.");
            }
        }

        public void OnSubmit()
        {
            if (_isSubmitting) return;

            string traineeName = nameInput != null ? nameInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(traineeName))
            {
                SetStatus("Please enter a name.");
                return;
            }

            string email = emailInput != null ? emailInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                SetStatus("Please enter a valid email.");
                return;
            }

            _isSubmitting = true;
            SetStatus("Creating trainee...");

            var manager = TrainingManager.Instance;
            if (manager == null)
            {
                SetStatus("TrainingManager is missing in the scene.");
                _isSubmitting = false;
                return;
            }

            manager.CreateTrainee(traineeName, email,
                onSuccess: () =>
                {
                    SetStatus("Creating session...");
                    if (flowManager != null)
                    {
                        bool ar = PracticalSessionVoice.IsArabicTrainingLanguage();
                        var prep = TrainingTtsText.Prepare(TrainingScreensVoice.PleaseReadInstructionsCarefully(ar));
                        TrainingInstructionsVoicePrefetch.EnsurePrefetchStarted(prep.text, prep.langCode, flowManager);
                    }
                    manager.CreateSession(defaultConfidenceBefore,
                        onSuccess: () =>
                        {
                            SetStatus("Registration complete.");
                            _isSubmitting = false;
                            // Move to the next visual step in the Training flow if configured.
                            if (flowManager != null)
                                flowManager.ShowTrainingInfo();
                            // At this point: TrainingManager.CurrentTraineeId and CurrentSessionId
                            // are set and stored in PlayerPrefs. Flow can move to info/CPR screens.
                        },
                        onError: err =>
                        {
                            SetStatus("Session error: " + err);
                            _isSubmitting = false;
                        });
                },
                onError: err =>
                {
                    SetStatus("Registration error: " + err);
                    _isSubmitting = false;
                });
        }

        void SetStatus(string msg)
        {
            if (statusLabel != null)
                statusLabel.text = msg;
            else
                Debug.Log($"AURAID Registration: {msg}");
        }
    }
}

