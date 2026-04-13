using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AURAID.UI
{
    public class UIScreenRefs : MonoBehaviour
    {
        [Header("Common")]
        public TMP_Text title;

        [Header("Buttons (use what exists on this screen)")]
        public Button buttonA;
        public Button buttonB;
        public Button buttonC; // optional (Language screen if you add 3 languages)
        [Tooltip("Optional: e.g. 'Previous' on Mode or Scenario screen to go back.")]
        public Button previousButton;

        [Header("Optional: button labels (for localization)")]
        public TMP_Text labelA;
        public TMP_Text labelB;
        public TMP_Text labelC;

        public void SetActive(bool isActive) => gameObject.SetActive(isActive);
    }
}
