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
using ThunderKit.Addressable.Tools;
using AddressableBrowserPlus = AssetsOfRain.Editor.Browser.AddressableBrowser;
using AssetsOfRain.Editor.VirtualAssets;

namespace AssetsOfRain.Editor.Browser
{
    public class AddressableBrowser : ThunderKit.Addressable.Tools.AddressableBrowser
    {
        private static readonly FieldInfo directoryContentField = typeof(ThunderKit.Addressable.Tools.AddressableBrowser).GetField("directoryContent", BindingFlags.Instance | BindingFlags.NonPublic);

        public override string Title => "Addressable Browser+";

        const string ADDRESSABLE_ASSET = "addressable-asset";
        const string BUTTON_PANEL = "addressable-button-panel";
        const string MANAGE_ASSET_BUTTON = "addressable-manage-asset-button";

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
            var assetLocation = (IResourceLocation)directoryContent.itemsSource[i];
            if (typeof(Shader).IsAssignableFrom(assetLocation.ResourceType) || typeof(SceneInstance).IsAssignableFrom(assetLocation.ResourceType))
            {
                return;
            }
            SerializableAssetRequest assetRequest = new SerializableAssetRequest { AssetLocation = assetLocation };
            bool assetAlreadyRequested = false;
            bool assetAlreadyExists = false;
            if (AssetsOfRainManager.TryGetInstance(out var manager))
            {
                assetAlreadyRequested = manager.virtualAssets.ContainsVirtualAsset(assetRequest);
                assetAlreadyExists = manager.virtualAssets.VirtualAssetExists(assetRequest);
            }

            VisualElement buttonPanel = element.Q(BUTTON_PANEL);

            Button manageAssetButton = buttonPanel.Q<Button>(MANAGE_ASSET_BUTTON);
            if (manageAssetButton == null)
            {
                manageAssetButton = new Button
                {
                    name = MANAGE_ASSET_BUTTON,
                };
                buttonPanel.Add(manageAssetButton);
            }

            if (assetAlreadyExists)
            {
                manageAssetButton.style.display = DisplayStyle.Flex;
                manageAssetButton.text = "Remove";
                manageAssetButton.tooltip = "Remove this addressable asset from the project";
                manageAssetButton.clickable = new Clickable(delegate ()
                {
                    var manager = AssetsOfRainManager.GetInstance();
                    manager.virtualAssets.DeleteVirtualAsset(assetRequest);
                    EditorUtility.SetDirty(manager);
                });
            }
            else if (!assetAlreadyRequested)
            {
                manageAssetButton.style.display = DisplayStyle.Flex;
                manageAssetButton.text = "Import";
                manageAssetButton.tooltip = "Add this addressable asset to the project";
                manageAssetButton.clickable = new Clickable(delegate ()
                {
                    var manager = AssetsOfRainManager.GetInstance();
                    manager.virtualAssets.ImportVirtualAsset(assetRequest);
                    EditorUtility.SetDirty(manager);
                });
            }
            else
            {
                manageAssetButton.style.display = DisplayStyle.None;
                manageAssetButton.clickable = null;
            }

            VisualElement assetPanel = element.Q(ADDRESSABLE_ASSET);
            assetPanel.RegisterCallback<PointerDownEvent>(OnPointerDown);

            void OnPointerDown(PointerDownEvent evt) 
            {
                if (AssetsOfRainManager.TryGetInstance(out var manager))
                {
                    var virtualAsset = manager.virtualAssets.GetVirtualAsset(assetRequest);
                    if (virtualAsset != null)
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new[] { virtualAsset };
                        DragAndDrop.paths = new[] { AssetDatabase.GetAssetPath(virtualAsset) };
                        DragAndDrop.StartDrag(assetRequest.primaryKey);
                    }
                }
            }
        }

        public ListView GetDirectoryContent() => (ListView)directoryContentField.GetValue(this);
    }
}
#endif