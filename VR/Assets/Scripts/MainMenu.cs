using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject menu;
    public GameObject settingsPanel;

    public GameObject practiceToggle;
    public GameObject theoryToggle;

    public bool activeMenu = true;
    
    void Start()
    {
        // Every field here is wired in the scene, and every one of them is a NullReferenceException
        // in Start if that wiring is ever lost - which takes the whole menu down, because a throw
        // here skips InitializeToggles and leaves the buttons unlistened.
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        InitializeToggles();
    }

    public void OpenLabScene()
    {
        Enter("LabScene");
    }

    public void OpenTestingPhaseLabScene()
    {
        Enter("TestingPhaseLab");
    }

    public void OpenLabAssistantScene()
    {
        Enter("LabAssistantScene");
    }

    /// <summary>
    /// Loads behind a fade rather than blocking the main thread. LabScene is 249 GameObjects and
    /// a synchronous load froze the window long enough for Windows to offer to close it.
    /// </summary>
    private void Enter(string sceneName)
    {
        AtomixAudio.UiClick();
        SceneTransition.Load(sceneName);
    }

    public void QuitApplication()
    {
        Debug.Log("Application Quitted!!!");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #endif
        Application.Quit();
        //UnityEditor.EditorApplication.isPlaying = false;
        //Application.Quit();
    }

    public void OpenSettingsPanel()
    {
        AtomixAudio.UiOpen();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void CloseSettingsPanel()
    {
        AtomixAudio.UiClose();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    void InitializeToggles()
    {
        if (practiceToggle == null || theoryToggle == null)
        {
            return;
        }

        if (StaticData.includedTasksValue == 0)
        {
            practiceToggle.GetComponent<Toggle>().isOn = true;
            theoryToggle.GetComponent<Toggle>().isOn = false;
            practiceToggle.GetComponent<Toggle>().interactable = false;
            theoryToggle.GetComponent<Toggle>().interactable = true;
        }
        else if (StaticData.includedTasksValue == 1)
        {
            practiceToggle.GetComponent<Toggle>().isOn = false;
            theoryToggle.GetComponent<Toggle>().isOn = true;
            theoryToggle.GetComponent<Toggle>().interactable = false;
            practiceToggle.GetComponent<Toggle>().interactable = true;
        }
        else if (StaticData.includedTasksValue == 2)
        {
            practiceToggle.GetComponent<Toggle>().isOn = true;
            theoryToggle.GetComponent<Toggle>().isOn = true;
            theoryToggle.GetComponent<Toggle>().interactable = true;
            practiceToggle.GetComponent<Toggle>().interactable = true;
        }

        practiceToggle.GetComponent<Toggle>().onValueChanged.AddListener(delegate { UpdateIncludedTaskTypes(); });
        theoryToggle.GetComponent<Toggle>().onValueChanged.AddListener(delegate { UpdateIncludedTaskTypes(); });
    }

    public void UpdateIncludedTaskTypes()
    {
        if (practiceToggle == null || theoryToggle == null)
        {
            return;
        }

        AtomixAudio.UiClick();

        if (practiceToggle.GetComponent<Toggle>().isOn && theoryToggle.GetComponent<Toggle>().isOn)
        {
            StaticData.includedTasksValue = 2;
            practiceToggle.GetComponent<Toggle>().interactable = true;
            theoryToggle.GetComponent<Toggle>().interactable = true;
        }
        else if (practiceToggle.GetComponent<Toggle>().isOn)
        {
            StaticData.includedTasksValue = 0;
            practiceToggle.GetComponent<Toggle>().interactable = false;
        }
        else if (theoryToggle.GetComponent<Toggle>().isOn)
        {
            StaticData.includedTasksValue = 1;
            theoryToggle.GetComponent<Toggle>().interactable = false;
        }
        else
        {
            StaticData.includedTasksValue = 0;
        }
        Debug.Log("Updated includedTaskTypes to: " + StaticData.includedTasksValue);
    }
}
