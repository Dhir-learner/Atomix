using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pushes the saved preferences into the live scene, and toasts achievements as they unlock.
///
/// <see cref="AtomixSettings"/> is a static store with no way to observe Unity's lifecycle. The
/// desktop rig it needs to configure is built at runtime by <see cref="DesktopBootstrap"/> after
/// every scene load, so the settings have to be re-applied then - and for a few frames afterwards,
/// because the controller is added during the same load and may not have run Start yet.
/// </summary>
public class AtomixSettingsApplier : MonoBehaviour
{
    [Tooltip("Seconds to keep re-applying after a scene load, while the desktop rig is still being built.")]
    public float settleSeconds = 2.0f;

    private float settleRemaining;

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AtomixSettings.Changed += HandleSettingsChanged;
        AchievementSystem.Unlocked += HandleAchievementUnlocked;
        settleRemaining = settleSeconds;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        AtomixSettings.Changed -= HandleSettingsChanged;
        AchievementSystem.Unlocked -= HandleAchievementUnlocked;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        settleRemaining = settleSeconds;
        AtomixSettings.Apply();
    }

    private void HandleSettingsChanged()
    {
        // AtomixSettings.Commit already calls Apply; this keeps the re-apply window open so a
        // change made mid-load still lands on the rig once it exists.
        settleRemaining = Mathf.Max(settleRemaining, 0.5f);
    }

    void Update()
    {
        if (settleRemaining <= 0.0f)
        {
            return;
        }

        settleRemaining -= Time.unscaledDeltaTime;
        AtomixSettings.Apply();
    }

    private void HandleAchievementUnlocked(Achievement achievement)
    {
        if (achievement == null)
        {
            return;
        }

        Debug.Log("[Atomix] Achievement unlocked: " + achievement.title);
        LabHudController.Toast("Achievement unlocked\n" + achievement.title);
    }
}
