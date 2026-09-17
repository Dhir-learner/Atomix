#if ATOMIX_ADS
using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Interstitial ads for the free mobile version: one full-screen ad after a successful experiment,
/// at most once every three minutes, shown only at a natural break - never over the graphs or the
/// post-success video, never mid-pour, never during a timed test. Nothing here ever blocks play:
/// no network, no consent or no fill simply means no ad.
/// </summary>
public class AtomixAds : MonoBehaviour
{
    // Paste the real ad unit IDs from the AdMob dashboard here. While they are empty, Google's
    // official test units are used, and AtomixMobileBuild refuses to make a store build.
    public const string AndroidInterstitialId = "";
    public const string IosInterstitialId = "";

    private const string AndroidTestInterstitialId = "ca-app-pub-3940256099942544/1033173712";
    private const string IosTestInterstitialId = "ca-app-pub-3940256099942544/4411468910";

    public const double MinSecondsBetweenAds = 180.0;
    private const float SettleSecondsAfterSuccess = 2.0f;
    private const float OwedAdExpirySeconds = 300.0f;
    private const float RetryLoadSeconds = 60.0f;
    private const string TestingSceneName = "TestingPhaseLab";
    private const string MainMenuSceneName = "MainMenuScene";
    private const string LastShownKey = "Atomix.Ads.LastShownUtcTicks";

    private static AtomixAds instance;

    private InterstitialAd interstitial;
    private bool initializeRequested;
    private bool initialized;
    private bool loading;
    private float retryLoadAt;
    private bool adOwed;
    private float owedAt;

    public static bool UsingTestAds
    {
        get
        {
#if UNITY_IOS
            return string.IsNullOrEmpty(IosInterstitialId);
#else
            return string.IsNullOrEmpty(AndroidInterstitialId);
#endif
        }
    }

    private static string InterstitialUnitId
    {
        get
        {
#if UNITY_IOS
            return UsingTestAds ? IosTestInterstitialId : IosInterstitialId;
#else
            return UsingTestAds ? AndroidTestInterstitialId : AndroidInterstitialId;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("AtomixAds");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AtomixAds>();
    }

    void Start()
    {
        ReactionHistoryRecorder.Completed += OnExperimentSucceeded;
        AgeGate.ShowIfNeeded(ConfigureAndInitialize);
    }

    void OnDestroy()
    {
        ReactionHistoryRecorder.Completed -= OnExperimentSucceeded;
        if (interstitial != null)
        {
            interstitial.Destroy();
            interstitial = null;
        }
    }

    // ------------------------------------------------------------------ setup

    private void ConfigureAndInitialize()
    {
        // Android pauses Unity during a full-screen ad; make iOS behave the same.
        MobileAds.SetiOSAppPauseOnBackground(true);

        bool child = AgeGate.UseChildTreatment;
        RequestConfiguration configuration = new RequestConfiguration
        {
            AgeRestrictedTreatment = child ? AgeRestrictedTreatment.Child : AgeRestrictedTreatment.Unspecified
        };
        if (child)
        {
            configuration.MaxAdContentRating = MaxAdContentRating.G;
        }
        MobileAds.SetRequestConfiguration(configuration);

        if (child)
        {
            // Consent must not be requested from children; child treatment already disables
            // personalised ads and the advertising ID.
            InitializeAds();
            return;
        }

        // A returning adult whose consent is already on file can start loading straight away.
        if (ConsentInformation.CanRequestAds())
        {
            InitializeAds();
        }

        ConsentRequestParameters parameters = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
        ConsentInformation.Update(parameters, (FormError updateError) =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (updateError != null)
                {
                    Debug.LogWarning("Atomix ads: consent update failed: " + updateError.Message);
                }

                if (ConsentInformation.CanRequestAds())
                {
                    InitializeAds();
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired((FormError showError) =>
                {
                    MobileAdsEventExecutor.ExecuteInUpdate(() =>
                    {
                        if (showError != null)
                        {
                            Debug.LogWarning("Atomix ads: consent form failed: " + showError.Message);
                        }
                        if (ConsentInformation.CanRequestAds())
                        {
                            InitializeAds();
                        }
                    });
                });
            });
        });
    }

    private void InitializeAds()
    {
        if (initializeRequested)
        {
            return;
        }
        initializeRequested = true;

        MobileAds.Initialize((InitializationStatus status) =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (status == null)
                {
                    Debug.LogWarning("Atomix ads: Google Mobile Ads failed to initialise.");
                    initializeRequested = false;
                    return;
                }

                initialized = true;
                LoadInterstitial();
            });
        });
    }

    private void LoadInterstitial()
    {
        if (loading)
        {
            return;
        }

        if (interstitial != null)
        {
            interstitial.Destroy();
            interstitial = null;
        }

        loading = true;
        InterstitialAd.Load(InterstitialUnitId, new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                loading = false;
                if (error != null || ad == null)
                {
                    retryLoadAt = Time.unscaledTime + RetryLoadSeconds;
                    return;
                }

                interstitial = ad;
                ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(LoadInterstitial);
                ad.OnAdFullScreenContentFailed += (AdError showError) =>
                    MobileAdsEventExecutor.ExecuteInUpdate(LoadInterstitial);
            });
        });
    }

    // ------------------------------------------------------------------ scheduling

    private void OnExperimentSucceeded(int reactionId, string reactionName, ExperimentOutcome outcome)
    {
        if (SceneManager.GetActiveScene().name == TestingSceneName || SecondsSinceLastAd() < MinSecondsBetweenAds)
        {
            return;
        }

        adOwed = true;
        owedAt = Time.unscaledTime;
    }

    void Update()
    {
        if (initialized && interstitial == null && !loading && Time.unscaledTime >= retryLoadAt)
        {
            LoadInterstitial();
        }

        if (!adOwed)
        {
            return;
        }

        if (Time.unscaledTime - owedAt > OwedAdExpirySeconds)
        {
            adOwed = false;
            return;
        }

        if (Time.unscaledTime - owedAt < SettleSecondsAfterSuccess ||
            interstitial == null || !interstitial.CanShowAd() || !IsNaturalBreak())
        {
            return;
        }

        adOwed = false;
        PlayerPrefs.SetString(LastShownKey, DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.Save();
        LabInput.ReleaseAllVirtual();
        interstitial.Show();
    }

    private static double SecondsSinceLastAd()
    {
        long ticks;
        if (!long.TryParse(PlayerPrefs.GetString(LastShownKey, string.Empty), out ticks))
        {
            return double.MaxValue;
        }
        return (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds;
    }

    private static bool IsNaturalBreak()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene == MainMenuSceneName)
        {
            return true;
        }
        if (scene == TestingSceneName)
        {
            return false;
        }

        if (!FirstPersonController.IsCursorLocked || LabTextInput.IsCapturing ||
            ReactionLearningController.IsAnyPanelVisible)
        {
            return false;
        }

        ReactionGraphUI graphs = FindAnyObjectByType<ReactionGraphUI>();
        if (graphs != null && graphs.IsOpen)
        {
            return false;
        }

        ExperimentHistoryUI history = FindAnyObjectByType<ExperimentHistoryUI>();
        if (history != null && history.IsOpen)
        {
            return false;
        }

        ObjectInteraction interaction = FindAnyObjectByType<ObjectInteraction>();
        return interaction == null || interaction.HeldObject == null;
    }
}
#endif
