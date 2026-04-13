using UnityEditor;
using UnityEngine;

public class ToggleActiveShortcut
{
    [MenuItem("Tools/Toggle Active %q")] // Ctrl + Q
    static void ToggleActive()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            obj.SetActive(!obj.activeSelf);
        }
    }
}