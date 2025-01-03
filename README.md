# Assets of Rain
Assets of Rain loads addressable assets from Risk of Rain 2 into the unity editor as assets which can be referenced by mods. Modded content is built as a secondary addressables catalog, and references to game assets in the editor resolve to the real assets at runtime.

Assets of Rain works entirely in the editor; no runtime package is required.

## Guide
If the addressables package was not installed, install com.unity.addressables version 1.18.15. If you are getting duplicate assembly warnings, re-import the game.

The Assets of Rain interface is a modified version of the ThunderKit adressable browser, found at `Tools/Assets of Rain/Addressable Browser+`. Use the browser to add and remove addressable assets from the project. All addresable shaders are automatically included.

The Addressable Browser+ also offers new sort options and the ability to drag-and-drop imported assets directly from the browser window.

To build a mod with Assets of Rain, add the new `AddressablesDefinition` datum to your mod's manifest. Use the Addressables package to add your modded assets to the build. Then, add the new `StageAddressables` job to your build pipeline.

`AddressablesDefinition.RuntimeLoadPath` needs to point to a static field at runtime. Here is an example where RuntimeLoadPath = `{MyNamespace.MyPlugin.AddressablesRuntimeDirectory}`:

```cs
namespace MyNamespace
{
    // ...
    public class MyPlugin : BaseUnityPlugin
    {
        public static string AddressablesRuntimeDirectory { get; private set; }

        void Awake()
        {
            string runtimeDirectory = Path.GetDirectoryName(Info.Location);
            AddressablesRuntimePath = Path.Combine(runtimeDirectory, "aa");

            // ...
        }
    }
}
```
*Assuming that your addressable assetbundles are  built to an `aa` folder

At runtime, call `Addressables.LoadContentCatalogAsync` to load your mod's content catalog. Afterwards you can use the standard Addressables methods to load your modded assets. Here is an example:
```cs
void Awake()
{
    string runtimeDirectory = Path.GetDirectoryName(Info.Location);
    string catalogPath = Path.Combine(runtimeDirectory, "aa", $"catalog_{MY_MOD_NAME}.json");
    Addressables.LoadContentCatalogAsync(catalogPath).WaitForCompletion();

    ItemDef exampleItem = Addressables.LoadAssetAsync<ItemDef>("MyMod/ExampleItem.asset").WaitForCompletion();
}
```
*Assuming that your content catalog is built to an `aa` folder

## Known Issues
* Materials with addressable shaders occasionally lose their shader referenced
* Many addressable shaders are grouped under the "Not Supported" shader tab
* Many addressable shaders are only partially functional or totally nonfunctional. This is likely because they rely on specific features of the RoR2 render pipeline
* Addressables builds will generate a useless unitybuiltinshaders bundle
* If for some reason an addressables build fails, the RoR2 addressables catalog imported by ThunderKit will disappear. This can be easily fixed with `Tools/Assets of Rain/Debug/Reimport Addressables Catalog`
* The additional search options in the Addressable Browser+ will sometimes disappear
* The errors "Could not Move to directory Library/com.unity.addressables/aa/Windows, directory arlready exists" and "Missing built-in guistyle ...*" appear in reload but seem to be harmless
* Imported addressable textures do not display transparency

## Acknowledgements
Assets of Rain is based on [BundleKit](https://github.com/foonix/BundleKit) by PassivePicasso and foonix ([BundleKit License](https://github.com/PassivePicasso/BundleKit/blob/master/LICENSE))

Special thanks to the developers in the ThunderKit discord for their guidance throughout this project.