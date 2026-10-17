using UnityEditor;
using UnityEngine;

public static class AddBoxColliders
{
    [MenuItem("Tools/Add Box Colliders To Selected")]
    static void AddToSelected()
    {
        int added = 0;

        foreach (GameObject root in Selection.gameObjects)
        {
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                GameObject target = mf.gameObject;

                if (target.GetComponent<Collider>() != null)
                {
                    continue;
                }

                Undo.AddComponent<BoxCollider>(target);
                added++;
            }
        }

        Debug.Log("Added " + added + " BoxColliders");
    }
}