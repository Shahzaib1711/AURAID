namespace AURAID.Emergency.CPR
{
    /// <summary>
    /// Single source of truth for emergency CPR enums — do not re-declare these types elsewhere.
    /// </summary>
    public enum PatientCategory
    {
        Infant,
        Child,
        Teenager,
        Adult
    }

    public enum EmergencyContext
    {
        Standard,
        Drowning,
        ShortnessOfBreath,
        DrugOverdose,
        Pregnancy,
        Trauma,
        PostSeizure,
        /// <summary>User unsure — CPR logic treats this the same as <see cref="Standard"/> after acknowledgment.</summary>
        Unknown
    }

    public enum CprQuality
    {
        Good,
        TooSlow,
        TooFast,
        TooLight,
        TooHard,
        IncompleteRecoil,
        PauseTooLong,
        /// <summary>Arm angle clearly off target while compressing.</summary>
        ArmsBent,
        /// <summary>Early posture hint before <see cref="ArmsBent"/> threshold.</summary>
        PostureNudge,
        PossibleMovementCheckBreathing
    }
}
