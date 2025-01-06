using UnityEditor;
using UnityEngine;

namespace AssetsOfRain.Editor.Fixes
{
    // Unity fixed a typo in the search styles but addressables still looks for the misspelled styles
    // Throws a few errors in the log without this fix
    [InitializeOnLoad]
    public static class MissingStylesFix
    {
        static MissingStylesFix()
        {
            GUISkin inspector = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
            if (inspector == null)
            {
                return;
            }

            RemapMissingStyle("ToolbarSeachTextFieldPopup", "ToolbarSearchTextFieldPopup");
            RemapMissingStyle("ToolbarSeachCancelButton", "ToolbarSearchCancelButton");
            RemapMissingStyle("ToolbarSeachCancelButtonEmpty", "ToolbarSearchCancelButtonEmpty");

            void RemapMissingStyle(string missingStyleName, string existingStyleName)
            {
                if (inspector.FindStyle(missingStyleName) != null)
                {
                    return;
                }
                GUIStyle existingStyle = inspector.FindStyle(existingStyleName);
                if (existingStyle != null)
                {
                    var customStyles = inspector.customStyles;
                    ArrayUtility.Add(ref customStyles, new GUIStyle(existingStyle)
                    {
                        name = missingStyleName
                    });
                    inspector.customStyles = customStyles;
                }
            }
        }
    }
}
