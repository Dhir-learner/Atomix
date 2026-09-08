using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CountdownTimer : MonoBehaviour
{
    float currentTime = 0;
    float startingTime = 60f;

    /// <summary>
    /// Coins earned in this run. Now read from <see cref="ExamSession"/> rather than held in a
    /// local field that Start() zeroed on every scene load - which is why the score used to vanish
    /// every time the test was restarted.
    /// </summary>
    public int Score { get { return ExamSession.RunCoins; } }

    /// <summary>Seconds left when the current task was stopped, for whoever files the result.</summary>
    public float SecondsLeftAtStop { get; private set; }

    /// <summary>How long the current task has been running.</summary>
    public float SecondsOnTask { get { return Mathf.Max(0f, startingTime - currentTime); } }

    /// <summary>Seconds left on the current task, for the report card and the pause menu.</summary>
    public float TimeRemaining { get { return Mathf.Max(0f, currentTime); } }
    public bool wasScored = false;
    public bool continua = true;
    public bool reset = false;

    [SerializeField] TMP_Text countdownText;
    [SerializeField] TMP_Text scoreText;      // Textul pentru scor
    [SerializeField] Randomize randomizer;

    // Start is called before the first frame update
    void Start()
    {
        currentTime = startingTime;
        RefreshScoreText();
    }

    /// <summary>Adds time to the clock - what the extra-time purchase buys.</summary>
    public void AddTime(float seconds)
    {
        currentTime += seconds;
        startingTime += seconds;
        continua = true;
    }

    void RefreshScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = ExamSession.RunCoins.ToString();
        }
    }

    // Update is called once per frame
    void Update()
    {
        RefreshScoreText();

        if (reset)
        {
            startingTime = 60f;
            currentTime = 60f;
            continua = true;
            reset = false;
            wasScored = false;
        }
        //currentTime -= 1 * Time.deltaTime;
        //if (currentTime >= 0 && continua)
        //{
        //    countdownText.text = Mathf.FloorToInt(currentTime).ToString();
        //}
        if (continua)
        {
            currentTime -= 1 * Time.deltaTime;
            if (currentTime >= 0)
            {
                countdownText.text = Mathf.FloorToInt(currentTime).ToString();
            }
            else
            {
                continua = false; // Stop the timer when it reaches zero
            }
        }
        if(!wasScored && currentTime <= 0)
        {
            continua = false;
            wasScored = true;
            SecondsLeftAtStop = 0f;
            randomizer.generateNewReaction = true;
        }
        // The award itself now lives in ExamSession.SpeedCoins, together with the streak and
        // first-clear bonuses. The tier table that used to sit here had an unreachable branch -
        // two consecutive `currentTime >= 30` tests - so the 50-coin tier could never fire and
        // everything finished with over 30 s left scored the maximum. See ExamSession.SpeedCoins.
        if (!continua && !wasScored && currentTime >= 0)
        {
            SecondsLeftAtStop = currentTime;
            wasScored = true;

            // Whoever stopped the clock files the task, because only they know what it was and
            // whether it passed. This just publishes the clock reading and refreshes the strip.
            RefreshScoreText();
        }
    }
}
