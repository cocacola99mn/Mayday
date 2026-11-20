using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Attaches the player scripts and assigns their Inspector references once.
/// Runs when Unity compiles, then does nothing once SampleScene is already wired.
/// </summary>
[InitializeOnLoad]
static class WirePlayerInput
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

    static int retries;

    static WirePlayerInput()
    {
        EditorApplication.delayCall += Wire;
    }

    static void Wire()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Wire;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Retry();
            return;
        }

        if (!IsSampleScene(scene))
            return;

        var move = FindAction("Player", "Move");
        var look = FindAction("Player", "Look");
        var sprint = FindAction("Player", "Sprint");
        var crouch = FindAction("Player", "Crouch");
        if (move == null || look == null || sprint == null || crouch == null)
        {
            Retry();
            if (retries > 8)
                Debug.LogError("Player wiring could not find Move, Look, Sprint, or Crouch on InputSystem_Actions.");
            return;
        }

        var player = GameObject.Find("Player");
        if (player == null)
        {
            if (scene.isDirty)
            {
                Debug.LogError("SampleScene has unsaved changes and no Player object. Reload the scene, then the player wiring will run.");
                return;
            }

            scene = EditorSceneManager.OpenScene(ScenePath);
            player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("SampleScene has no Player object to wire.");
                return;
            }
        }

        var cameraRoot = player.transform.Find("CameraRoot");
        var cameraBob = cameraRoot != null ? cameraRoot.Find("CameraBob") : null;
        if (cameraRoot == null || cameraBob == null)
        {
            Debug.LogError("Player is missing CameraRoot or CameraBob.");
            return;
        }

        var movement = player.GetComponent<PlayerMovement>() ?? Undo.AddComponent<PlayerMovement>(player);
        var lookBehaviour = player.GetComponent<PlayerLook>() ?? Undo.AddComponent<PlayerLook>(player);
        var headBob = cameraBob.GetComponent<HeadBob>() ?? Undo.AddComponent<HeadBob>(cameraBob.gameObject);

        int ceilingMask = Physics.DefaultRaycastLayers;
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            ceilingMask &= ~(1 << playerLayer);

        bool changed = false;
        changed |= SetObject(movement, "moveAction", move);
        changed |= SetObject(movement, "sprintAction", sprint);
        changed |= SetObject(movement, "crouchAction", crouch);
        changed |= SetObject(movement, "cameraRoot", cameraRoot);
        changed |= SetInt(movement, "ceilingMask", ceilingMask);

        changed |= SetObject(lookBehaviour, "lookAction", look);
        changed |= SetObject(lookBehaviour, "moveAction", move);
        changed |= SetObject(lookBehaviour, "sprintAction", sprint);
        changed |= SetObject(lookBehaviour, "cameraRoot", cameraRoot);

        changed |= SetObject(headBob, "movement", movement);

        if (!changed)
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Wired the player: Move, Look, Sprint, Crouch, CameraRoot, and the ceiling mask.");
    }

    static void Retry()
    {
        if (retries++ > 8)
            return;
        EditorApplication.delayCall += Wire;
    }

    static bool IsSampleScene(Scene scene)
    {
        return scene.path.Replace('\\', '/') == ScenePath;
    }

    static InputActionReference FindAction(string mapName, string actionName)
    {
        string expectedName = mapName + "/" + actionName;
        var assets = AssetDatabase.LoadAllAssetsAtPath(ActionsPath);
        InputActionReference hiddenFallback = null;

        foreach (var asset in assets)
        {
            if (asset is not InputActionReference reference || reference.name != expectedName)
                continue;

            if ((reference.hideFlags & HideFlags.HideInHierarchy) == 0)
                return reference;

            hiddenFallback = reference;
        }

        return hiddenFallback;
    }

    static bool SetObject(Object target, string propertyName, Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"No '{propertyName}' field on {target.GetType().Name}.");
            return false;
        }

        if (property.objectReferenceValue == value)
            return false;

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    static bool SetInt(Object target, string propertyName, int value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"No '{propertyName}' field on {target.GetType().Name}.");
            return false;
        }

        if (property.intValue == value)
            return false;

        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
