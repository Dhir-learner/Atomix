#if ATOMIX_ADS
using System;
using TMPro;
using UnityEngine;

/// <summary>
/// The neutral age screen Google Play's Families policy requires for a mixed-audience app with ads.
/// It asks for a birth year without suggesting an answer, stores it once, and never asks again,
/// so a child cannot simply retry with an older year.
///
/// Anyone under 16 - or who prefers not to say - gets child treatment: that covers COPPA's
/// under-13 rule and every EEA country's age of digital consent, which tops out at 16.
/// </summary>
public static class AgeGate
{
    private const string BirthYearKey = "Atomix.AgeGate.BirthYear";
    private const int NotAnswered = 0;
    private const int PreferNotToSay = -1;
    private const int ChildTreatmentUnderAge = 16;

    public static bool HasAnswered
    {
        get { return PlayerPrefs.GetInt(BirthYearKey, NotAnswered) != NotAnswered; }
    }

    /// <summary>True for under-16s and for anyone whose age is unknown.</summary>
    public static bool UseChildTreatment
    {
        get
        {
            int birthYear = PlayerPrefs.GetInt(BirthYearKey, NotAnswered);
            if (birthYear <= 0)
            {
                return true;
            }

            // Without a birth date, assume the birthday has not come yet this year.
            int age = DateTime.UtcNow.Year - birthYear - 1;
            return age < ChildTreatmentUnderAge;
        }
    }

    /// <summary>Calls <paramref name="onAnswered"/> at once if already answered, otherwise after the screen.</summary>
    public static void ShowIfNeeded(Action onAnswered)
    {
        if (HasAnswered)
        {
            onAnswered();
            return;
        }

        GameObject host = new GameObject("AgeGate");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<AgeGateScreen>().Build(onAnswered);
    }

    internal static void Save(int birthYearOrPreferNotToSay)
    {
        PlayerPrefs.SetInt(BirthYearKey, birthYearOrPreferNotToSay);
        PlayerPrefs.Save();
    }

    internal const int PreferNotToSayValue = PreferNotToSay;
}

internal class AgeGateScreen : MonoBehaviour
{
    private const int EarliestYear = 1920;

    private Action onAnswered;
    private int year;
    private bool chosen;
    private TMP_Text yearText;
    private UnityEngine.UI.Button continueButton;

    public void Build(Action answered)
    {
        onAnswered = answered;
        year = DateTime.UtcNow.Year;

        Canvas canvas;
        UnityEngine.UI.CanvasScaler scaler;
        RectTransform panel = LabPanelBuilder.CreateFullScreenCanvas(transform, "AgeGateCanvas",
            new Vector2(1920.0f, 1080.0f), 900, 180.0f, out canvas, out scaler);

        LabPanelBuilder.CreateText("Title", panel, new Vector2(0.0f, 260.0f), new Vector2(1200.0f, 80.0f),
            "Before you start", 54.0f, TextAlignmentOptions.Center, Color.white);
        LabPanelBuilder.CreateText("Question", panel, new Vector2(0.0f, 170.0f), new Vector2(1200.0f, 70.0f),
            "What year were you born?", 40.0f, TextAlignmentOptions.Center, Color.white);

        // Starts blank rather than on a plausible adult year, so the screen never suggests an answer.
        yearText = LabPanelBuilder.CreateText("Year", panel, new Vector2(0.0f, 40.0f), new Vector2(400.0f, 120.0f),
            "----", 90.0f, TextAlignmentOptions.Center, Color.white);

        Vector2 stepSize = new Vector2(150.0f, 100.0f);
        LabPanelBuilder.CreateButton("Minus10", panel, "-10", new Vector2(-480.0f, 40.0f), stepSize, 36.0f, () => Step(-10));
        LabPanelBuilder.CreateButton("Minus1", panel, "-1", new Vector2(-310.0f, 40.0f), stepSize, 36.0f, () => Step(-1));
        LabPanelBuilder.CreateButton("Plus1", panel, "+1", new Vector2(310.0f, 40.0f), stepSize, 36.0f, () => Step(1));
        LabPanelBuilder.CreateButton("Plus10", panel, "+10", new Vector2(480.0f, 40.0f), stepSize, 36.0f, () => Step(10));

        continueButton = LabPanelBuilder.CreateButton("Continue", panel, "Continue", new Vector2(-220.0f, -160.0f),
            new Vector2(380.0f, 110.0f), 38.0f, Confirm);
        continueButton.interactable = false;

        LabPanelBuilder.CreateButton("PreferNotToSay", panel, "Prefer not to say", new Vector2(220.0f, -160.0f),
            new Vector2(380.0f, 110.0f), 34.0f, () => Finish(AgeGate.PreferNotToSayValue));

        LabPanelBuilder.CreateText("Note", panel, new Vector2(0.0f, -280.0f), new Vector2(1300.0f, 80.0f),
            "This is only used to show age-appropriate ads. You will not be asked again.",
            28.0f, TextAlignmentOptions.Center, AtomixSettings.BodyTextColour);
    }

    private void Step(int delta)
    {
        if (!chosen)
        {
            chosen = true;
            continueButton.interactable = true;
        }

        year = Mathf.Clamp(year + delta, EarliestYear, DateTime.UtcNow.Year);
        yearText.text = year.ToString();
    }

    private void Confirm()
    {
        if (chosen)
        {
            Finish(year);
        }
    }

    private void Finish(int value)
    {
        AgeGate.Save(value);
        Destroy(gameObject);
        onAnswered();
    }
}
#endif
