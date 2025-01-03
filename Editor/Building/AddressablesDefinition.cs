using ThunderKit.Core.Attributes;
using ThunderKit.Core.Manifests;
using UnityEngine;

namespace AssetsOfRain.Editor.Building
{
    public class AddressablesDefinition : ManifestDatum
    {
        [Tooltip("Evaluated at runtime to determine the location of your addressable asset bundles. Surround static field or property references with braces, e.g., {MyNamespace.MyClass.addressablesRuntimePath}")]
        [PathReferenceResolver]
        public string RuntimeLoadPath;
    }
}