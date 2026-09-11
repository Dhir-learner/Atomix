using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Randomize : MonoBehaviour
{
    [SerializeField] CountdownTimer countdown;
    public GameObject randomizer;
    public int currentReactionInTestPhase;
    public bool generateNewReaction;
    public TMP_Text canvasText;
    public GameObject canvasForReactionTask;
    public GameObject canvasForTheoreticalTask;

    GameObject container_CuO;
    GameObject correct_erlenmeyer_with_substance;
    GameObject erlenmeyer_hcl;
    GameObject berzelius_hcl_nahco3;
    GameObject correct_berzelius_with_substance;
    GameObject container_nahco3;
    GameObject NatriumContainer;
    GameObject container_fenolftaleina_full;
    GameObject correct_berzelius_with_substance_NaOH;
    GameObject correct_erlenmeyer_with_substance_H2O;
    public GameObject NewNatriumContainer;

    GameObject Water;
    GameObject Metiloranj;
    GameObject PotassiumContainer;
    public GameObject NewPotassiumContainer;
    GameObject KOHContainer;

    GameObject TubeWithSubstance;
    GameObject pipette;
    GameObject small_vase_new;
    GameObject SuportEprubeta1;
    GameObject TurnesolPaper;
    GameObject waterBeaker;
    GameObject BunsenBurner;
    GameObject water_ali3;
    GameObject container_aluminum;
    GameObject CaOH_beaker22;
    GameObject BunsenBurner1;
    GameObject containerCaO;
    GameObject container_iodine;
    GameObject TubeWithSubstance1;
    GameObject SuportEprubeta;
    GameObject Balon;

    List<int> reactionsDone;
    public bool stopTesting;

    void Start()
    {
        //StaticData.includedTasksValue = 1;
        //StaticData.includedTasksValue = 2;
        FindAllObjects();
        generateNewReaction = true;
        //currentReactionInTestPhase = 5;
        stopTesting = false;
        reactionsDone = new List<int>();
        //Debug.Log(StaticData.includedTasksValue);
    }

    /// <summary>How many different tasks a mode can hand out: practical, theory, or both.</summary>
    static int TaskPoolSize(int includedTasksValue)
    {
        switch (includedTasksValue)
        {
            case 1: return 10;   // theory questions 9-18
            case 2: return 18;   // both
            default: return 8;   // the eight practical experiments
        }
    }

    /// <summary>
    /// Ends the run after its last task is over. Leaves the screen as it always ended - the
    /// reaction-task canvas up with "You have finished all the tasks!" on it, the question canvas
    /// down - and puts the last task's equipment away, so its script stops writing to the canvas
    /// and closes its attempt the same way every earlier task's did.
    /// </summary>
    void FinishRun()
    {
        stopTesting = true;
        generateNewReaction = false;

        HideAllObjects();
        if (canvasForReactionTask != null)
        {
            canvasForReactionTask.SetActive(true);
        }

        if (countdown != null)
        {
            countdown.continua = false;
        }
    }

    void HideAllObjects()
    {
        List<GameObject> objectsToHide = new List<GameObject> {
            container_CuO,
            correct_erlenmeyer_with_substance,
            erlenmeyer_hcl,
            berzelius_hcl_nahco3,
            correct_berzelius_with_substance,
            container_nahco3,
            NatriumContainer,
            container_fenolftaleina_full,
            correct_berzelius_with_substance_NaOH,
            correct_erlenmeyer_with_substance_H2O,
            Water,
            Metiloranj,
            PotassiumContainer,
            NewPotassiumContainer,
            KOHContainer,
            NewNatriumContainer,
            canvasForReactionTask,
            canvasForTheoreticalTask,
            TubeWithSubstance,
            pipette,
            small_vase_new,
            SuportEprubeta1,
            TurnesolPaper,
            waterBeaker,
            BunsenBurner,
            water_ali3,
            container_aluminum,
            CaOH_beaker22,
            BunsenBurner1,
            containerCaO,
            container_iodine,
            TubeWithSubstance1,
            SuportEprubeta,
            Balon
        };

        foreach (GameObject obj in objectsToHide)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }

    void FindAllObjects()
    {
        container_CuO = GameObject.Find("container_CuO");
        correct_erlenmeyer_with_substance = GameObject.Find("correct_erlenmeyer-with-substance");
        erlenmeyer_hcl = GameObject.Find("erlenmeyer-hcl");
        berzelius_hcl_nahco3 = GameObject.Find("berzelius_hcl_nahco3");
        correct_berzelius_with_substance = GameObject.Find("correct_berzelius-with-substance");
        container_nahco3 = GameObject.Find("container_nahco3");
        NatriumContainer = GameObject.Find("NatriumContainer");
        container_fenolftaleina_full = GameObject.Find("container_fenolftaleina_full");
        correct_berzelius_with_substance_NaOH = GameObject.Find("correct_berzelius-with-substance_NaOH");
        correct_erlenmeyer_with_substance_H2O = GameObject.Find("correct_erlenmeyer-with-substance_H2O");
        Water = GameObject.Find("Water");
        Metiloranj = GameObject.Find("Metiloranj");
        PotassiumContainer = GameObject.Find("PotassiumContainer");
        KOHContainer = GameObject.Find("KOHContainer");

        TubeWithSubstance = GameObject.Find("TubeWithSubstance");
        pipette = GameObject.Find("pipette");
        small_vase_new = GameObject.Find("small_vase_new");
        SuportEprubeta1 = GameObject.Find("SuportEprubeta1");
        TurnesolPaper = GameObject.Find("TurnesolPaper");
        waterBeaker = GameObject.Find("waterBeaker");
        BunsenBurner = GameObject.Find("BunsenBurner");
        water_ali3 = GameObject.Find("water_ali3");
        container_aluminum = GameObject.Find("container_aluminum");
        CaOH_beaker22 = GameObject.Find("CaOH_beaker22");
        BunsenBurner1 = GameObject.Find("BunsenBurner1");
        containerCaO = GameObject.Find("containerCaO");
        container_iodine = GameObject.Find("container_iodine");
        TubeWithSubstance1 = GameObject.Find("TubeWithSubstance1");
        SuportEprubeta = GameObject.Find("SuportEprubeta");
        Balon = GameObject.Find("Balon");
    }

    void Update()
    {
        if (stopTesting)
        {
            canvasText.text = "You have finished all the tasks!";
        }
        else
        {
            if (generateNewReaction)
            {
                // A new task is only asked for once the current one is over - finished, failed,
                // skipped or out of time. So this is the moment to end the run, if every task the
                // student asked for has now been played. The run used to be ended while the last
                // task was being handed out instead, which skipped it: its equipment appeared for
                // a single frame, the report card opened over it, and it was filed as timed out.
                int requested = Mathf.Clamp(StaticData.TaskCount, 1, TaskPoolSize(StaticData.includedTasksValue));
                if (reactionsDone.Count >= requested)
                {
                    FinishRun();
                    return;
                }

                HideAllObjects();
                if (StaticData.includedTasksValue == 0)
                {
                    do
                    {
                        currentReactionInTestPhase = Random.Range(1, 9);
                    } while (reactionsDone.Contains(currentReactionInTestPhase));
                }
                else if (StaticData.includedTasksValue == 1)
                {
                    do
                    {
                        currentReactionInTestPhase = Random.Range(9, 19);
                    } while (reactionsDone.Contains(currentReactionInTestPhase));
                }
                else if (StaticData.includedTasksValue == 2)
                {
                    do
                    {
                        currentReactionInTestPhase = Random.Range(1, 19);
                    } while (reactionsDone.Contains(currentReactionInTestPhase));
                }
                reactionsDone.Add(currentReactionInTestPhase);

                // How many tasks there are in total comes from the setup screen (it used to be a
                // hard-coded 10), clamped to how many the chosen mode has. Checked above, when
                // the next task is asked for - not here, where the task is still to be played.
                Debug.Log($"Generated Random Number: {currentReactionInTestPhase}");
                if (currentReactionInTestPhase < 9)
                {
                    canvasForReactionTask.SetActive(true);
                }
                else
                {
                    canvasForTheoreticalTask.SetActive(true);
                }
                countdown.reset = true;
                if (currentReactionInTestPhase == 1) //Na + H2O + phenolphtalein
                {
                    List<GameObject> objectsToShow = new List<GameObject> {
                        correct_berzelius_with_substance_NaOH,
                        correct_erlenmeyer_with_substance_H2O,
                        NatriumContainer,
                        container_fenolftaleina_full,
                    };

                    foreach (GameObject obj in objectsToShow)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(true);
                        }
                    }
                }
                else if (currentReactionInTestPhase == 2) //hcl+nahco3
                {
                    List<GameObject> objectsToShow = new List<GameObject> {
                        erlenmeyer_hcl,
                        berzelius_hcl_nahco3,
                        container_nahco3,
                    };

                    foreach (GameObject obj in objectsToShow)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(true);
                        }
                    }
                }
                else if (currentReactionInTestPhase == 3) //CuO+h2so4
                {
                    List<GameObject> objectsToShow = new List<GameObject> {
                        correct_berzelius_with_substance,
                        correct_erlenmeyer_with_substance,
                        container_CuO,
                    };

                    foreach (GameObject obj in objectsToShow)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(true);
                        }
                    }
                }
                else if (currentReactionInTestPhase == 4) // KOH + H2O + methyl orange
                {
                    List<GameObject> objectsToShow = new List<GameObject> {
                        KOHContainer,
                        Water,
                        PotassiumContainer,
                        Metiloranj,
                    };

                    foreach (GameObject obj in objectsToShow)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(true);
                        }
                    }
                }
                else if (currentReactionInTestPhase == 5) // aluminiu
                {
                    List<GameObject> objectsToShow = new List<GameObject> {
                        pipette,
                        small_vase_new,
                        water_ali3,
                        container_aluminum,
                        container_iodine
                    };

                    foreach (GameObject obj in objectsToShow)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(true);
                        }
                    }
                }
                else if (currentReactionInTestPhase == 6) // cao+h2o
                {
                    List<GameObject> objectsToShow = new List<GameObject> {
                        TurnesolPaper,
                        waterBeaker,
                        CaOH_beaker22,
                        containerCaO
                    };

                    foreach (GameObject obj in objectsToShow)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(true);
                        }
                    }
                }
                else if (currentReactionInTestPhase == 7) // caco3
                {
                    List<GameObject> objectsToShow = new List<GameObject> {
                        SuportEprubeta1,
                        BunsenBurner1,
                        TubeWithSubstance1,
                        Balon
                    };

                    foreach (GameObject obj in objectsToShow)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(true);
                        }
                    }
                }
                else if (currentReactionInTestPhase == 8) // feso4
                {
                    List<GameObject> objectsToShow = new List<GameObject> {
                        TubeWithSubstance,
                        BunsenBurner,
                        SuportEprubeta
                    };

                    foreach (GameObject obj in objectsToShow)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(true);
                        }
                    }
                }
                generateNewReaction = false;
            }
        }
    }
}
