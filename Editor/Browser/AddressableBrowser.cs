#if TK_ADDRESSABLE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ThunderKit.Common;
using ThunderKit.Core.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using static ThunderKit.Core.UIElements.TemplateHelpers;
using ThunderKit.Addressable.Tools;
using AddressableBrowserPlus = AssetsOfRain.Editor.Browser.AddressableBrowser;

namespace AssetsOfRain.Editor.Browser
{
    public class AddressableBrowser : ThunderKit.Addressable.Tools.AddressableBrowser
    {
        private static readonly FieldInfo directoryContentField = typeof(ThunderKit.Addressable.Tools.AddressableBrowser).GetField("directoryContent", BindingFlags.Instance | BindingFlags.NonPublic);

        public override string Title => "Addressable Browser+";

        public override void OnEnable()
        {
            base.OnEnable();

            AddressableGraphicsSettings.AddressablesInitialized -= ModifyBrowser;
            AddressableGraphicsSettings.AddressablesInitialized += ModifyBrowser;

            ModifyBrowser();
        }

        public new void OnDisable()
        {
            AddressableGraphicsSettings.AddressablesInitialized -= ModifyBrowser;

            base.OnDisable();
        }

        public void ModifyBrowser(object sender = null, EventArgs e = null)
        {
            Debug.Log("Modify browser");
            ListView directoryContent = GetDirectoryContent();
            directoryContent.bindItem = (Action<VisualElement, int>)Delegate.Combine(directoryContent.bindItem, new Action<VisualElement, int>(ModifyAsset));
        }

        public void ModifyAsset(VisualElement element, int i)
        {
            ListView directoryContent = GetDirectoryContent();
            var location = (IResourceLocation)directoryContent.itemsSource[i];
            string virtualAssetPath = AssetsOfRainManager.GetVirtualAssetPath(location.PrimaryKey);
            bool assetInProject = AssetDatabase.GetMainAssetTypeAtPath(virtualAssetPath) == location.ResourceType;

            VisualElement buttonPanel = element.Q("addressable-button-panel");

            Button manageAssetButton = buttonPanel.Q<Button>("addressable-manage-asset-button");
            if (manageAssetButton == null)
            {
                manageAssetButton = new Button
                {
                    name = "addressable-manage-asset-button",
                };
                buttonPanel.Add(manageAssetButton);
            }
            if (assetInProject)
            {
                manageAssetButton.text = "Import";
                manageAssetButton.tooltip = "Add this addressable asset to the project";
            }
            VisualElement assetPanel = element.Q("addressable-asset");
            assetPanel.RegisterCallback<PointerDownEvent>(OnPointerDown);

            void OnPointerDown(PointerDownEvent evt) 
            {
                DragAndDrop.PrepareStartDrag();

                // Store reference to object and path to object in DragAndDrop static fields.
                //DragAndDrop.objectReferences = new[] { CreateInstance<ItemDef>() };
                DragAndDrop.paths = Array.Empty<string>();

                // Start a drag.
                DragAndDrop.StartDrag(string.Empty);
            }
        }

        public ListView GetDirectoryContent() => (ListView)directoryContentField.GetValue(this);
    }
}
#endif