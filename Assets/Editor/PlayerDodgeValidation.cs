using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PlayerDodgeValidation
{
    private static readonly List<InputDevice> pausedDevices = new List<InputDevice>();
    private static Keyboard keyboard;
    private static Gamepad gamepad;
    private static InputSettings originalSettings, testSettings;
    private static bool originalBackground;

    public static void ValidateAssets()
    {
        foreach (string path in new[] { "Assets/Scenes/System/Boot.unity", BossEncounterSetup.ScenePath })
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            PlayerDodge dodge = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerDodge>();
            Check(dodge != null && dodge.Distance == .3f && dodge.MotionDuration == .5f &&
                dodge.LandingRecovery == .2f, $"Dodge: authored values in {path}");
        }
    }

    public static IEnumerator RunChecks()
    {
        StartInputs();
        EditorSceneManager.LoadSceneInPlayMode(BossEncounterSetup.ScenePath,
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return .12f;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Rigidbody body = player.GetComponent<Rigidbody>();
        PlayerDodge dodge = player.GetComponent<PlayerDodge>();
        PlayerCombat combo = player.GetComponent<PlayerCombat>();
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        foreach (EnemySpawnGroup group in Object.FindObjectsByType<EnemySpawnGroup>()) group.gameObject.SetActive(false);
        foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>()) enemy.gameObject.SetActive(false);
        CompanionManager manager = CompanionManager.Instance;
        manager.enabled = false;
        for (int i = 0; i < manager.GetCompanionCount(); i++) manager.GetCompanion(i).gameObject.SetActive(false);
        Vector3 origin = new Vector3(70, 1.02f, 0);
        Reset(player, origin);
        yield return .06f;
        Check(!dodge.RequestDodge(0f) && !dodge.RequestDodge(.1f), "Dodge: neutral and slight stick drift cannot trigger evasion");
        Vector3 start = body.position;
        float started = Time.time;
        Check(dodge.RequestDodge(1f) && dodge.IsMoving && !body.useGravity,
            "Dodge: right input starts the lateral hop");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
        yield return .25f;
        Check(Mathf.Abs(body.position.x - start.x - .15f) < .035f &&
            Mathf.Abs(body.position.z - start.z) < .02f && body.position.y > start.y + .08f,
            "Dodge: halfway position follows the hop despite movement and jump input");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Capture(player);
        GameObject attacker = SpawnBody(new Vector3(100, 1, 20));
        int hp = health.CurrentHP;
        BattleSystem.Instance.Attack(attacker, player, 10, null);
        Check(health.CurrentHP == hp - 10 && dodge.IsDodging && player.GetComponent<Collider>().enabled,
            "Dodge: enemy damage still applies during evasion, with the player collider enabled");
        Object.Destroy(attacker);
        yield return new EnemyAlphaValidation.Until(() => dodge.Phase == PlayerDodge.DodgePhase.Recovering,
            "Dodge: landing starts recovery");
        Check(Time.time - started >= .49f && Mathf.Abs(body.position.x - start.x - .3f) < .025f,
            "Dodge: unobstructed movement takes 0.5 seconds and travels 0.3 metres");
        float landed = Time.time;
        Check(!dodge.RequestDodge(-1f) && !combo.RequestAttack(), "Dodge: landing recovery blocks evasion and attacks");
        yield return .1f;
        Check(dodge.Phase == PlayerDodge.DodgePhase.Recovering, "Dodge: landing recovery is not released early");
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsDodging, "Dodge: landing recovery ends");
        Check(Time.time - landed >= .18f && body.useGravity,
            "Dodge: recovery lasts 0.2 seconds and restores normal gravity");
        start = body.position;
        Check(dodge.RequestDodge(-1f), "Dodge: recovery completion permits immediate reuse without another cooldown");
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsDodging, "Dodge: left hop completes");
        Check(Mathf.Abs(body.position.x - start.x + .3f) < .025f, "Dodge: left hop travels the same 0.3 metres");

        Reset(player, origin, Quaternion.Euler(0, 90, 0));
        dodge.RequestDodge(1f);
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsDodging, "Dodge: rotated player finishes a hop");
        Check(Mathf.Abs(body.position.z - origin.z + .3f) < .025f && Mathf.Abs(body.position.x - origin.x) < .02f,
            "Dodge: sideways is measured from the player's facing rather than world axes");

        Reset(player, origin);
        GameObject target = SpawnBody(origin + Vector3.forward * 2.05f);
        int targetHP = target.GetComponent<EnemyHealth>().CurrentHP;
        Check(combo.RequestAttack() && combo.RequestAttack(), "Dodge: a combo starts with a buffered follow-up");
        yield return .06f;
        Check(dodge.RequestDodge(1f) && !combo.IsAttacking && combo.ComboStep == 0 && !combo.HasBufferedAttack,
            "Dodge: evasion cancels the current attack, combo stage and buffered input");
        Check(!combo.RequestAttack(), "Dodge: attacks cannot start during evasion");
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsDodging, "Dodge: attack-cancelling hop completes");
        Check(target.GetComponent<EnemyHealth>().CurrentHP == targetHP,
            "Dodge: the cancelled pre-impact attack never deals delayed damage");
        Check(combo.RequestAttack() && combo.ComboStep == 1, "Dodge: the next attack restarts the combo at one");
        combo.CancelCombo();
        Object.Destroy(target);
        yield return .04f;

        Reset(player, origin + Vector3.up * 3f);
        combo.RequestAttack();
        yield return .08f;
        Check(dodge.RequestDodge(1f) && !combo.IsAttacking, "Dodge: an airborne attack can also be cancelled");
        float airStarted = Time.time;
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsDodging,
            "Dodge: airborne motion releases controls immediately without recovery");
        float stoppedX = body.position.x;
        Check(Time.time - airStarted >= .49f && Time.time - airStarted < .6f && body.useGravity &&
            body.position.y > origin.y + 1f, "Dodge: aerial dodge keeps its motion duration but has no falling lockout");
        Check(combo.RequestAttack(), "Dodge: an attack is accepted immediately after aerial dodge motion");
        combo.CancelCombo();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        yield return .08f;
        Check(!dodge.ControlsMovement && body.linearVelocity.z > 3f && Mathf.Abs(body.position.x - stoppedX) < .01f,
            "Dodge: normal movement resumes during the fall after an aerial dodge");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return new EnemyAlphaValidation.Until(() => body.position.y < origin.y + .03f,
            "Dodge: aerial dodge reaches the ground normally");
        Check(dodge.Phase == PlayerDodge.DodgePhase.Idle && dodge.RequestDodge(-1f),
            "Dodge: landing after aerial dodge adds no delayed recovery and permits immediate reuse");
        dodge.CancelDodge();

        Reset(player, origin);
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = origin + new Vector3(.65f, 1, 0);
        wall.transform.localScale = new Vector3(.1f, 5, 5);
        Physics.SyncTransforms();
        dodge.RequestDodge(1f);
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsMoving, "Dodge: a wall interrupts lateral motion");
        stoppedX = body.position.x;
        Check(stoppedX <= origin.x + .11f && stoppedX >= origin.x && body.useGravity && body.linearVelocity.y <= 0f,
            "Dodge: wall contact stops the hop before penetration and starts falling");
        yield return .08f;
        Check(Mathf.Abs(body.position.x - stoppedX) < .01f, "Dodge: no continued sliding through a wall");
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsDodging, "Dodge: blocked hop still finishes landing recovery");
        Reset(player, origin + Vector3.up * 2f);
        Check(dodge.RequestDodge(1f), "Dodge: aerial evasion starts next to a wall");
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsMoving,
            "Dodge: wall contact also interrupts an aerial dodge");
        Check(!dodge.IsDodging && body.useGravity && body.position.y > origin.y + 1f && combo.RequestAttack(),
            "Dodge: blocked aerial dodge releases attacks immediately without falling or landing lockout");
        combo.CancelCombo();
        Object.Destroy(wall);
        yield return .04f;

        Reset(player, origin);
        target = SpawnBody(origin + Vector3.right * 1.65f);
        targetHP = target.GetComponent<EnemyHealth>().CurrentHP;
        Physics.SyncTransforms();
        dodge.RequestDodge(1f);
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsMoving, "Dodge: an enemy body interrupts lateral motion");
        Check(body.position.x < origin.x + .16f && body.useGravity && target.GetComponent<EnemyHealth>().CurrentHP == targetHP,
            "Dodge: enemy contact blocks motion without damaging the enemy");
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsDodging, "Dodge: enemy-blocked recovery completes");
        Object.Destroy(target);
        yield return .04f;

        Reset(player, origin);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.LeftShift));
        yield return new EnemyAlphaValidation.Until(() => dodge.IsMoving, "Dodge: A plus left Shift triggers evasion through the real input system");
        yield return .8f;
        Check(!dodge.IsDodging, "Dodge: holding a chord does not automatically repeat evasion");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return .04f;
        Reset(player, origin);
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up }.WithButton(GamepadButton.East));
        yield return .12f;
        Check(!dodge.IsDodging && body.linearVelocity.z > 5.5f, "Dodge: forward plus B retains the existing run control");
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.left }.WithButton(GamepadButton.East));
        yield return new EnemyAlphaValidation.Until(() => dodge.IsMoving, "Dodge: turning the stick sideways while holding B starts evasion");
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.North));
        yield return .06f;
        Check(!combo.IsAttacking, "Dodge: gamepad Y cannot start an attack during evasion");
        InputSystem.QueueStateEvent(gamepad, new GamepadState());
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsDodging, "Dodge: gamepad left hop completes");
        Reset(player, origin);
        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.East));
        yield return new EnemyAlphaValidation.Until(() => dodge.IsMoving, "Dodge: rightward left-stick input plus B starts the right hop");
        InputSystem.QueueStateEvent(gamepad, new GamepadState());
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsDodging, "Dodge: gamepad right hop completes");
        Check(Mathf.Abs(body.position.x - origin.x - .3f) < .035f, "Dodge: gamepad hop preserves the specified distance");

        Reset(player, origin + Vector3.up * 3f);
        dodge.RequestDodge(1f);
        yield return .1f;
        dodge.enabled = false;
        Check(!dodge.IsDodging && body.useGravity && player.GetComponent<MeshRenderer>().enabled,
            "Dodge: disabling evasion restores gravity and the original player model");
        dodge.enabled = true;
        Reset(player, origin);
        dodge.RequestDodge(-1f);
        health.TakeDamage(health.MaxHP);
        yield return new EnemyAlphaValidation.Until(() => !dodge.IsDodging, "Dodge: lethal damage cancels evasion");
        Check(!dodge.RequestDodge(1f) && !combo.RequestAttack() && body.useGravity,
            "Dodge: a defeated player cannot evade or attack and keeps normal physics");
        RestoreInputs();
    }

    private static GameObject SpawnBody(Vector3 position)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyC.prefab"),
            position, Quaternion.identity);
        root.GetComponent<EnemyController>().enabled = false;
        root.GetComponent<EnemyCombat>().enabled = false;
        Rigidbody body = root.GetComponent<Rigidbody>();
        body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
        return root;
    }

    private static void Reset(GameObject player, Vector3 position, Quaternion? rotation = null)
    {
        player.GetComponent<PlayerDodge>().CancelDodge();
        player.GetComponent<PlayerCombat>().CancelCombo();
        Rigidbody body = player.GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.position = position;
        body.rotation = rotation ?? Quaternion.identity;
        player.transform.SetPositionAndRotation(position, body.rotation);
        Physics.SyncTransforms();
    }

    private static void Capture(GameObject player)
    {
        Check(player.transform.Find("DodgeModel").gameObject.activeSelf &&
            !player.GetComponent<MeshRenderer>().enabled,
            "Dodge: the visible hop animates the model without rotating or disabling collision");
        Camera camera = player.GetComponentInChildren<Camera>();
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        pixels.Apply();
        Directory.CreateDirectory(".utmp");
        File.WriteAllBytes(".utmp/player-dodge.png", pixels.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        target.Release();
        Object.Destroy(target);
        Object.Destroy(pixels);
    }

    private static void StartInputs()
    {
        originalBackground = Application.runInBackground;
        Application.runInBackground = true;
        originalSettings = InputSystem.settings;
        testSettings = Object.Instantiate(originalSettings);
        testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = testSettings;
        foreach (InputDevice device in InputSystem.devices) if (device.enabled) pausedDevices.Add(device);
        foreach (InputDevice device in pausedDevices) InputSystem.DisableDevice(device);
        keyboard = InputSystem.AddDevice<Keyboard>();
        gamepad = InputSystem.AddDevice<Gamepad>();
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode) RestoreInputs();
    }

    private static void RestoreInputs()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
        keyboard = null; gamepad = null;
        foreach (InputDevice device in pausedDevices) if (device.added) InputSystem.EnableDevice(device);
        pausedDevices.Clear();
        if (originalSettings != null) InputSystem.settings = originalSettings;
        originalSettings = null;
        if (testSettings != null) Object.Destroy(testSettings);
        testSettings = null;
        Application.runInBackground = originalBackground;
    }

    private static void Check(bool condition, string description) => EnemyAlphaValidation.Check(condition, description);
}
