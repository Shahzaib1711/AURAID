using UnityEditor;
using UnityEngine;

namespace AURAID.Editor
{
    /// <summary>Opens the TMP Font Asset Creator with a reminder of the Arabic character set and atlas size.</summary>
    public static class CreateArabicFontAsset
    {
        const string CharacterSequence = "0600-06FF,0020,005F,FE70-FEFF";
        const int AtlasSize = 2048;

        [MenuItem("Tools/AURAID/Open Arabic Font Creator", false, 100)]
        static void OpenArabicFontCreator()
        {
            EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Font Asset Creator");
            Debug.Log($"AURAID: Use Source Font = Noto Sans Arabic, Character Set = Unicode Range (Hex), Character Sequence = {CharacterSequence}, Atlas Resolution = {AtlasSize}x{AtlasSize}. See Assets/Scripts/UI/ArabicFontSetup.md.");
        }
    }
}
