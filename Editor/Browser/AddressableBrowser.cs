#if TK_ADDRESSABLE
using AssetsOfRain.Editor.VirtualAssets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThunderKit.Addressable.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.UIElements;

namespace AssetsOfRain.Editor.Browser
{
    // Same name as the TK class so we can reuse the style sheet
    public class AddressableBrowser : ThunderKit.Addressable.Tools.AddressableBrowser
    {
        [Flags]
        public enum BrowserOptionsExtended
        {
            None,
            ShowType = 1 << 0,
            ShowProvider = 1 << 1,
            IgnoreCase = 1 << 2,
            UseRegex = 1 << 3,
            InProject = 1 << 4,
            Available = 1 << 5,
            ExtendedFlags = InProject | Available,
        }

        public enum AssetStatus
        {
            NeverAvailable,
            InProject,
            Available,
            Neutral,
        }

        private static readonly FieldInfo directoryContentField = typeof(ThunderKit.Addressable.Tools.AddressableBrowser).GetField("directoryContent", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo directoryField = typeof(ThunderKit.Addressable.Tools.AddressableBrowser).GetField("directory", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo DirectoryContent_onSelectionChanged = typeof(ThunderKit.Addressable.Tools.AddressableBrowser).GetMethod("DirectoryContent_onSelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo GroupLocation = typeof(ThunderKit.Addressable.Tools.AddressableBrowser).GetMethod("GroupLocation", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DirectoryContentsField = typeof(ThunderKit.Addressable.Tools.AddressableBrowser).GetField("DirectoryContents", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo m_CallbackRegistry = typeof(CallbackEventHandler).GetField("m_CallbackRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
        
        public override string Title => "Addressable Browser+";

        const string ADDRESSABLE_ASSET = "addressable-asset";
        const string BUTTON_PANEL = "addressable-button-panel";
        const string MANAGE_ASSET_BUTTON = "addressable-manage-asset-button";
        const string DISPLAY_OPTIONS = "display-options";

        public BrowserOptionsExtended browserOptionsExtended;
        private Dictionary<string, List<IResourceLocation>> FullDirectoryContents;

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

        // Called after InitializeBrowser
        public void ModifyBrowser(object sender = null, EventArgs e = null)
        {
            ListView directoryContent = GetDirectoryContent();

            directoryContent.bindItem = (Action<VisualElement, int>)Delegate.Combine(directoryContent.bindItem, new Action<VisualElement, int>(ModifyAsset));
            
            var onSelectionChanged = (Action<IEnumerable<object>>)Delegate.CreateDelegate(typeof(Action<IEnumerable<object>>), this, DirectoryContent_onSelectionChanged);
            directoryContent.onSelectionChange -= onSelectionChanged;
            directoryContent.onItemsChosen += onSelectionChanged;

            var displayOptionsField = rootVisualElement.Q<EnumFlagsField>(DISPLAY_OPTIONS);
            displayOptionsField.Init((BrowserOptionsExtended)browserOptions);
            displayOptionsField.RegisterValueChangedCallback(OnOptionsChanged);

            FullDirectoryContents = null;
            RefreshAdditionalOptions(BrowserOptionsExtended.None, (BrowserOptionsExtended)browserOptions);
        }

        // Called after BindAsset
        public void ModifyAsset(VisualElement element, int i)
        {
            ListView directoryContent = GetDirectoryContent();
            var assetLocation = (IResourceLocation)directoryContent.itemsSource[i];
            var assetStatus = GetAssetStatus(assetLocation, out var assetRequest);
            if (assetStatus == AssetStatus.NeverAvailable)
            {
                return;
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

            switch (assetStatus)
            {
                case AssetStatus.Available:
                    manageAssetButton.style.display = DisplayStyle.Flex;
                    manageAssetButton.style.backgroundColor = new StyleColor(new Color32(49, 123, 150, 255));
                    manageAssetButton.text = "Import";
                    manageAssetButton.tooltip = "Add this addressable asset to the project";
                    manageAssetButton.clickable = new Clickable(delegate ()
                    {
                        var manager = AssetsOfRainManager.GetInstance();
                        manager.virtualAssets.ImportVirtualAsset(assetRequest);
                        EditorUtility.SetDirty(manager);
                        directoryContent?.RefreshItems();
                    });
                    break;
                case AssetStatus.InProject:
                    manageAssetButton.style.display = DisplayStyle.Flex;
                    manageAssetButton.style.backgroundColor = new StyleColor(new Color32(181, 56, 81, 255));
                    manageAssetButton.text = "Remove";
                    manageAssetButton.tooltip = "Remove this addressable asset from the project";
                    manageAssetButton.clickable = new Clickable(delegate ()
                    {
                        var manager = AssetsOfRainManager.GetInstance();
                        var virtualAssetPath = AssetDatabase.GetAssetPath(manager.virtualAssets.GetVirtualAsset(assetRequest));
                        if (!string.IsNullOrEmpty(virtualAssetPath))
                        {
                            AssetDatabase.SaveAssets();
                            List<string> dependentAssets = AssetDatabase.GetAllAssetPaths()
                            .Where(x => Array.IndexOf(AssetDatabase.GetDependencies(x, false), virtualAssetPath) >= 0)
                            .ToList();
                            dependentAssets.Remove(virtualAssetPath);
                            if (dependentAssets.Count > 0)
                            {
                                string message = $@"The following assets will be left with missing references:

{string.Join('\n', dependentAssets)}";

                                if (!EditorUtility.DisplayDialog("Remove addressable asset?", message, "Remove", "Cancel"))
                                {
                                    return;
                                }
                            }
                        }
                        manager.virtualAssets.DeleteVirtualAsset(assetRequest);
                        EditorUtility.SetDirty(manager);
                        directoryContent?.RefreshItems();
                    });
                    break;
                case AssetStatus.Neutral:
                    manageAssetButton.style.display = DisplayStyle.None;
                    manageAssetButton.clickable = null;
                    break;
            }

            VisualElement assetPanel = element.Q(ADDRESSABLE_ASSET);
            m_CallbackRegistry.SetValue(assetPanel, null);
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

        public void OnOptionsChanged(ChangeEvent<Enum> evt)
        {
            RefreshAdditionalOptions((BrowserOptionsExtended)evt.previousValue, (BrowserOptionsExtended)browserOptions);
        }

        public void RefreshAdditionalOptions(BrowserOptionsExtended previousValue, BrowserOptionsExtended newValue)
        {
            BrowserOptionsExtended deltaValue = previousValue ^ newValue;
            if ((deltaValue & BrowserOptionsExtended.ExtendedFlags) == BrowserOptionsExtended.None)
            {
                return;
            }
            bool inProject = (newValue & BrowserOptionsExtended.InProject) > BrowserOptionsExtended.None;
            bool available = (newValue & BrowserOptionsExtended.Available) > BrowserOptionsExtended.None;
            if (!inProject && !available)
            {
                if (FullDirectoryContents != null)
                {
                    DirectoryContentsWrapper = FullDirectoryContents;
                    FullDirectoryContents = null;
                }
            }
            else
            {
                FullDirectoryContents ??= DirectoryContentsWrapper;
                var filteredDirectoryContents = FullDirectoryContents
                    .Select(Filter)
                    .Where(x => x.Value != null && x.Value.Count > 0);

                DirectoryContentsWrapper = new Dictionary<string, List<IResourceLocation>>(filteredDirectoryContents);

                KeyValuePair<string, List<IResourceLocation>> Filter(KeyValuePair<string, List<IResourceLocation>> keyValuePair)
                {
                    var filteredResourceLocations = new List<IResourceLocation>(keyValuePair.Value.Where(IncludeAssetLocation));
                    return new KeyValuePair<string, List<IResourceLocation>>(keyValuePair.Key, filteredResourceLocations);
                }

                bool IncludeAssetLocation(IResourceLocation assetLocation) => GetAssetStatus(assetLocation, out _) switch
                {
                    AssetStatus.InProject => inProject,
                    AssetStatus.Available => available,
                    _ => false,
                };
            }

            // Attempt to re-select the current selection to refresh the content list
            IResourceLocation g = GetDirectory().selectedItems.OfType<IResourceLocation>().FirstOrDefault();
            if (g != null)
            {
                string selected = GroupLocationWrapper(g);
                EditorApplication.update += ReturnDirectorySelection;

                void ReturnDirectorySelection()
                {
                    EditorApplication.update -= ReturnDirectorySelection;
                    var directory = GetDirectory();
                    for (int i = 0; i < directory.itemsSource.Count; i++)
                    {
                        if (directory.itemsSource[i] is IResourceLocation item && GroupLocationWrapper(item) == selected)
                        {
                            directory.SetSelection(i);
                            return;
                        }
                    }
                }
            }
        }

        public AssetStatus GetAssetStatus(IResourceLocation assetLocation, out SerializableAssetRequest assetRequest)
        {
            if (typeof(Shader).IsAssignableFrom(assetLocation.ResourceType) || typeof(SceneInstance).IsAssignableFrom(assetLocation.ResourceType))
            {
                assetRequest = default;
                return AssetStatus.NeverAvailable;
            }
            assetRequest = new SerializableAssetRequest { AssetLocation = assetLocation };
            bool assetAlreadyRequested = false;
            bool assetAlreadyExists = false;
            if (AssetsOfRainManager.TryGetInstance(out var manager))
            {
                assetAlreadyRequested = manager.virtualAssets.ContainsVirtualAsset(assetRequest);
                assetAlreadyExists = manager.virtualAssets.VirtualAssetExists(assetRequest);
            }
            if (assetAlreadyExists)
            {
                return AssetStatus.InProject;
            }
            else if (!assetAlreadyRequested)
            {
                return AssetStatus.Available;
            }
            else
            {
                return AssetStatus.Neutral;
            }
        }

        public ListView GetDirectoryContent() => (ListView)directoryContentField.GetValue(this);
        public ListView GetDirectory() => (ListView)directoryField.GetValue(this);

        public string GroupLocationWrapper(IResourceLocation g) => (string)GroupLocation.Invoke(this, new object[] { g });

        public Dictionary<string, List<IResourceLocation>> DirectoryContentsWrapper 
        {
            get => (Dictionary<string, List<IResourceLocation>>)DirectoryContentsField.GetValue(this);
            set => DirectoryContentsField.SetValue(this, value);
        }
    }
}
#endif