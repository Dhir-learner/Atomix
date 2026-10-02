using UnityEditor;

/// <summary>
/// Lets the phone controls be tried in the Editor without a phone.
///
/// The Device Simulator (Window > General > Device Simulator) already turns them on by itself,
/// because it reports a mobile platform, and it turns mouse clicks into touches. This switch forces
/// them on in the ordinary Game view too - useful for checking the HUD layout - though without
/// the simulator there are no touches to drive them.
/// </summary>
public static class AtomixMobileMenu
{
    private const string MenuPath = "Tools/Atomix/Simulate Mobile Controls";

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        bool enabled = !EditorPrefs.GetBool(AtomixInput.SimulateMobilePrefKey, false);
        EditorPrefs.SetBool(AtomixInput.SimulateMobilePrefKey, enabled);
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(AtomixInput.SimulateMobilePrefKey, false));

        // Read once when play starts, so changing it mid-session would only half apply.
        return !EditorApplication.isPlaying;
    }
}
