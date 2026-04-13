using System;
using AURAID.Emergency.CPR;
using UnityEngine;

namespace AURAID.Emergency
{
    /// <summary>
    /// Collects patient category + emergency context from UI (or other input), writes them to
    /// <see cref="CprRulesConfig"/>, and pushes the config to <see cref="RuleBasedCprAgent"/>.
    /// Attach to EmergencyRoot (or same object as the agent). Wire buttons to
    /// <see cref="SetPatientCategory"/>, <see cref="SetContext"/>, <see cref="ApplyToAgent"/>.
    /// </summary>
    public class EmergencyInputManager : MonoBehaviour
    {
        [Header("References (Inspector)")]
        [Tooltip("CPR rule agent that receives config updates.")]
        [SerializeField] RuleBasedCprAgent agent;
        [Tooltip("ScriptableObject holding thresholds; patientCategory and context are updated at runtime.")]
        [SerializeField] CprRulesConfig config;
        [Tooltip("Optional. Receives input-received / applied notifications for timeout + sensor gating.")]
        [SerializeField] EmergencyController emergencyController;

        [Header("Fallback when no UI selection")]
        [Tooltip("Used for pending state on first enable and when calling ApplyDefaults.")]
        [SerializeField] PatientCategory defaultCategory = PatientCategory.Adult;
        [SerializeField] EmergencyContext defaultContext = EmergencyContext.Standard;

        /// <summary>Fired after a successful user <see cref="ApplyToAgent"/> (not after <see cref="ApplyDefaults"/> from OnEnable).</summary>
        public event Action AppliedToAgent;

        bool _hasUserSelected;
        bool _isApplied;

        PatientCategory _patientCategory;
        EmergencyContext _emergencyContext;

        void Awake()
        {
            if (agent == null)
                agent = GetComponent<RuleBasedCprAgent>();
            if (agent == null)
                agent = FindObjectOfType<RuleBasedCprAgent>();
            if (emergencyController == null)
                emergencyController = GetComponentInParent<EmergencyController>();
            if (emergencyController == null)
                emergencyController = FindObjectOfType<EmergencyController>();

            _patientCategory = defaultCategory;
            _emergencyContext = defaultContext;
        }

        void OnEnable()
        {
            if (!_hasUserSelected)
                ApplyDefaults();
        }

        void MarkUserTouchedInput()
        {
            _hasUserSelected = true;
            _isApplied = false;
            emergencyController?.NotifyEmergencyInputReceived();
        }

        /// <summary>Sets the patient type (Infant / Child / Teen / Adult). Call <see cref="ApplyToAgent"/> after UI flow completes.</summary>
        public void SetPatientCategory(PatientCategory category)
        {
            _patientCategory = category;
            MarkUserTouchedInput();
        }

        /// <summary>Sets what happened (maps to <see cref="EmergencyContext"/>). <see cref="EmergencyContext.Unknown"/> is stored as Standard on the config for CPR logic.</summary>
        public void SetContext(EmergencyContext context)
        {
            _emergencyContext = context;
            MarkUserTouchedInput();
        }

        /// <summary>Writes pending category/context to config and calls <see cref="RuleBasedCprAgent.UpdateConfig"/>.</summary>
        public void ApplyToAgent()
        {
            if (config == null || agent == null)
            {
                Debug.LogWarning("[EmergencyInputManager] Assign CprRulesConfig and RuleBasedCprAgent on EmergencyRoot.");
                return;
            }

            if (_isApplied)
            {
                Debug.Log("[EmergencyInputManager] ApplyToAgent skipped (already applied). Change patient/context, then apply again.");
                return;
            }

            _isApplied = true;
            _hasUserSelected = true;
            PushConfigCore();
            Debug.Log($"[EmergencyInputManager] AURAID Config Applied → Category: {config.patientCategory}, Context: {config.context}");
            AppliedToAgent?.Invoke();
        }

        void PushConfigCore()
        {
            if (config == null || agent == null) return;
            config.patientCategory = _patientCategory;
            config.context = ResolveContextForConfig(_emergencyContext);
            agent.UpdateConfig(config);
        }

        static EmergencyContext ResolveContextForConfig(EmergencyContext context)
        {
            return context == EmergencyContext.Unknown ? EmergencyContext.Standard : context;
        }

        /// <summary>Reset pending values to inspector defaults and push to agent (e.g. timeout or Reset button). Does not fire <see cref="AppliedToAgent"/>.</summary>
        public void ApplyDefaults()
        {
            _hasUserSelected = false;
            _isApplied = false;
            _patientCategory = defaultCategory;
            _emergencyContext = defaultContext;

            if (config == null || agent == null)
            {
                Debug.LogWarning("[EmergencyInputManager] ApplyDefaults: missing CprRulesConfig or RuleBasedCprAgent.");
                return;
            }

            PushConfigCore();
            Debug.Log($"[EmergencyInputManager] AURAID Defaults pushed → Category: {config.patientCategory}, Context: {config.context}");
        }

        #region UI helpers (UnityEvent-friendly)

        /// <summary>Dropdown / button: 0=Infant, 1=Child, 2=Teenager, 3=Adult.</summary>
        public void SetPatientCategoryByIndex(int index)
        {
            if (index < 0 || index > 3) return;
            SetPatientCategory((PatientCategory)index);
        }

        /// <summary>Dropdown: use enum order (see <see cref="EmergencyContext"/>).</summary>
        public void SetContextByIndex(int index)
        {
            var values = (EmergencyContext[])Enum.GetValues(typeof(EmergencyContext));
            if (index < 0 || index >= values.Length) return;
            SetContext(values[index]);
        }

        public void SetContextStandard() => SetContext(EmergencyContext.Standard);
        public void SetContextDrowning() => SetContext(EmergencyContext.Drowning);
        public void SetContextShortnessOfBreath() => SetContext(EmergencyContext.ShortnessOfBreath);
        public void SetContextDrugOverdose() => SetContext(EmergencyContext.DrugOverdose);
        public void SetContextPregnancy() => SetContext(EmergencyContext.Pregnancy);
        public void SetContextTrauma() => SetContext(EmergencyContext.Trauma);
        public void SetContextPostSeizure() => SetContext(EmergencyContext.PostSeizure);
        public void SetContextUnknown() => SetContext(EmergencyContext.Unknown);

        public void SetPatientInfant() => SetPatientCategory(PatientCategory.Infant);
        public void SetPatientChild() => SetPatientCategory(PatientCategory.Child);
        public void SetPatientTeenager() => SetPatientCategory(PatientCategory.Teenager);
        public void SetPatientAdult() => SetPatientCategory(PatientCategory.Adult);

        #endregion
    }
}
