using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerDodgeSetup
{
    // Add only the new behaviour; do not regenerate the existing arenas or player settings.
    [MenuItem("Tools/Player/Apply Dodge To Player Scenes")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (string path in new[] { "Assets/Scenes/System/Boot.unity", BossEncounterSetup.ScenePath })
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (PlayerController player in root.GetComponentsInChildren<PlayerController>(true))
                {
                    PlayerDodge dodge = player.GetComponent<PlayerDodge>();
                    if (dodge == null) dodge = player.gameObject.AddComponent<PlayerDodge>();
                    EditorUtility.SetDirty(dodge);
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
