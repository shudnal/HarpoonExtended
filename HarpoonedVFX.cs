using UnityEngine;
using static UnityEngine.Object;

namespace HarpoonExtended
{
    public class HarpoonedVFX
    {
        public const string prefabName = "vfx_Harpooned_HarpoonExtended";
        public static GameObject prefab;

        public static GameObject CreateEffect(Vector3 position, Transform parent)
        {
            if (!prefab)
            {
                prefab = CustomPrefabs.InitPrefabClone(ZNetScene.instance.GetPrefab("vfx_Harpooned"), prefabName);
                DestroyImmediate(prefab.GetComponent<ZNetView>());
                DestroyImmediate(prefab.GetComponent<ZSyncTransform>());
            }

            return Instantiate(prefab, position, Quaternion.identity, parent);
        }
    }
}
