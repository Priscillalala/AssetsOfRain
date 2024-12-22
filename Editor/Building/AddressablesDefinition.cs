using ThunderKit.Core.Attributes;
using ThunderKit.Core.Manifests;

namespace AssetsOfRain.Editor.Building
{
    public class AddressablesDefinition : ManifestDatum
    {
        [PathReferenceResolver]
        public string RuntimeLoadPath;
    }
}