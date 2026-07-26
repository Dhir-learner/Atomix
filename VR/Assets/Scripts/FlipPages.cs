using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FlipPages : MonoBehaviour
{
    [SerializeField] float pageSpeed = 0.6f;
    [SerializeField] public List<Transform> pages;
    int index = -1;
    bool rotate = false;
    [SerializeField] GameObject backButton;
    [SerializeField] GameObject forwardButton;
    public GameObject popupWindow;
    public TMP_Text canvasText;

    public GameObject bookCanvas;
    public GameObject titlu1;
    public GameObject titlu2;
    public GameObject titlu3;

    public GameObject page1reaction1text;
    public GameObject page1reaction1button;
    public GameObject page1reaction2text;
    public GameObject page1reaction2button;
    public GameObject page2reaction1text;
    public GameObject page2reaction1button;
    public GameObject page2reaction2text;
    public GameObject page2reaction2button;

    public GameObject page0reaction1text;
    public GameObject page0reaction1button;
    public GameObject page0reaction2text;
    public GameObject page0reaction2button;

    [SerializeField] public int selectedReaction = -1;
    private readonly List<int> keyboardReactionSequence = new List<int>();
    private readonly HashSet<GameObject> mappedKeyboardButtons = new HashSet<GameObject>();

    private void Start()
    {
        InitialState();
        BuildKeyboardReactionSequence();
    }

    private void Update()
    {
        if (bookCanvas == null || !bookCanvas.activeInHierarchy)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) && forwardButton.activeInHierarchy)
        {
            RotateForward();
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow) && backButton.activeInHierarchy)
        {
            RotateBack();
        }

        HandleNumericReactionShortcuts();
    }

    void HandleNumericReactionShortcuts()
    {
        for (int shortcut = 1; shortcut <= 8; shortcut++)
        {
            if (!IsNumberShortcutPressed(shortcut))
            {
                continue;
            }

            TriggerReactionShortcut(shortcut);
            break;
        }
    }

    bool IsNumberShortcutPressed(int shortcut)
    {
        switch (shortcut)
        {
            case 1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            case 2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            case 3: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
            case 4: return Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);
            case 5: return Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5);
            case 6: return Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6);
            case 7: return Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7);
            case 8: return Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8);
            default: return false;
        }
    }

    void TriggerReactionShortcut(int shortcut)
    {
        if (keyboardReactionSequence.Count == 0)
        {
            BuildKeyboardReactionSequence();
        }

        int sequenceIndex = shortcut - 1;
        int reactionId = sequenceIndex < keyboardReactionSequence.Count
            ? keyboardReactionSequence[sequenceIndex]
            : shortcut;

        SelectReactionById(reactionId);
    }

    void BuildKeyboardReactionSequence()
    {
        keyboardReactionSequence.Clear();
        mappedKeyboardButtons.Clear();

        AddButtonToKeyboardSequence(page0reaction1button);
        AddButtonToKeyboardSequence(page0reaction2button);
        AddButtonToKeyboardSequence(page1reaction1button);
        AddButtonToKeyboardSequence(page1reaction2button);
        AddButtonToKeyboardSequence(page2reaction1button);
        AddButtonToKeyboardSequence(page2reaction2button);

        if (bookCanvas != null)
        {
            Button[] sceneButtons = bookCanvas.GetComponentsInChildren<Button>(true);
            List<KeyValuePair<int, GameObject>> extraMappings = new List<KeyValuePair<int, GameObject>>();

            foreach (Button sceneButton in sceneButtons)
            {
                if (sceneButton == null || mappedKeyboardButtons.Contains(sceneButton.gameObject))
                {
                    continue;
                }

                if (!TryGetReactionIdFromButton(sceneButton, out int reactionId))
                {
                    continue;
                }

                extraMappings.Add(new KeyValuePair<int, GameObject>(reactionId, sceneButton.gameObject));
            }

            extraMappings.Sort((left, right) => left.Key.CompareTo(right.Key));

            foreach (KeyValuePair<int, GameObject> mapping in extraMappings)
            {
                AddReactionToKeyboardSequence(mapping.Key, mapping.Value);
            }
        }

        // Safety fallback in case scene wiring changes or references are missing.
        if (keyboardReactionSequence.Count == 0)
        {
            for (int reactionId = 1; reactionId <= 8; reactionId++)
            {
                keyboardReactionSequence.Add(reactionId);
            }
        }
    }

    void AddButtonToKeyboardSequence(GameObject buttonObject)
    {
        if (buttonObject == null)
        {
            return;
        }

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        if (!TryGetReactionIdFromButton(button, out int reactionId))
        {
            return;
        }

        AddReactionToKeyboardSequence(reactionId, buttonObject);
    }

    void AddReactionToKeyboardSequence(int reactionId, GameObject buttonObject)
    {
        if (reactionId < 1 || reactionId > 8 || keyboardReactionSequence.Contains(reactionId))
        {
            return;
        }

        keyboardReactionSequence.Add(reactionId);

        if (buttonObject != null)
        {
            mappedKeyboardButtons.Add(buttonObject);
        }
    }

    bool TryGetReactionIdFromButton(Button button, out int reactionId)
    {
        reactionId = -1;

        if (button == null)
        {
            return false;
        }

        Button.ButtonClickedEvent clickEvent = button.onClick;
        int listenerCount = clickEvent.GetPersistentEventCount();

        for (int i = 0; i < listenerCount; i++)
        {
            UnityEngine.Object target = clickEvent.GetPersistentTarget(i);
            if (target != this)
            {
                continue;
            }

            string methodName = clickEvent.GetPersistentMethodName(i);
            if (string.IsNullOrEmpty(methodName) || !methodName.StartsWith("Reaction", StringComparison.Ordinal))
            {
                continue;
            }

            string numericPart = methodName.Substring("Reaction".Length);
            if (int.TryParse(numericPart, out reactionId))
            {
                return true;
            }
        }

        return false;
    }

    void SelectReactionById(int reactionId)
    {
        switch (reactionId)
        {
            case 1: Reaction1(); break;
            case 2: Reaction2(); break;
            case 3: Reaction3(); break;
            case 4: Reaction4(); break;
            case 5: Reaction5(); break;
            case 6: Reaction6(); break;
            case 7: Reaction7(); break;
            case 8: Reaction8(); break;
        }
    }

    bool TryOpenReactionLearningFlow(int reactionId)
    {
        ReactionLearningController learningController = ReactionLearningController.GetOrCreate(this);
        if (learningController == null)
        {
            return false;
        }

        return learningController.TryRequestReaction(reactionId);
    }

    public void StartReactionExperiment(int reactionId)
    {
        selectedReaction = reactionId;
        closeTheBook();
    }

    public void InitialState()
    {
        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].transform.rotation = Quaternion.identity;
        }

        titlu1.transform.rotation = Quaternion.Euler(new Vector3(0, 180, 0));
        TextMeshProUGUI textMeshPro = titlu1.GetComponent<TextMeshProUGUI>();
        if (textMeshPro != null)
        {
            textMeshPro.text = "Decomposition Reactions";
        }

        index = -1;
        pages[0].SetAsLastSibling();
        backButton.SetActive(false);
        forwardButton.SetActive(true);
        page1reaction1text.SetActive(false);
        page1reaction1button.SetActive(false);
        page1reaction2text.SetActive(false);
        page1reaction2button.SetActive(false);

        page0reaction1button.SetActive(true);
        page0reaction2button.SetActive(true);
        page0reaction1text.SetActive(true);
        page0reaction2text.SetActive(true);
    }

    public void closeTheBook()
    {
        ReactionLearningController.NotifyBookClosed(gameObject.scene);
        InitialState();

        BookCanvasManager bookManager = bookCanvas.GetComponent<BookCanvasManager>();
        if (bookManager != null)
        {
            bookManager.CloseBook();
        }
        else
        {
            bookCanvas.SetActive(false);
        }

        if (canvasText.text == "Now you can search through the book for the desired reaction by clicking on the Next and Previous buttons. When you have decided, click on the Play button.")
        {
            popupWindow.SetActive(false);
        }
    }

    public void RotateForward()
    {
        if (rotate == true) { return; }
        index++;
        float angle = 180;
        ForwardButtonActions();
        pages[index].SetAsLastSibling();
        StartCoroutine(Rotate(angle, true));
    }

    public void ForwardButtonActions()
    {
        if (backButton.activeInHierarchy == false)
        {
            backButton.SetActive(true);
        }

        if (index == pages.Count - 1)
        {
            forwardButton.SetActive(false);
        }

        if (index == 0)
        {
            titlu1.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
            TextMeshProUGUI textMeshPro = titlu1.GetComponent<TextMeshProUGUI>();
            if (textMeshPro != null)
            {
                textMeshPro.text = "Displacement Reactions";
            }

            page0reaction1button.SetActive(false);
            page0reaction2button.SetActive(false);
            page0reaction1text.SetActive(false);
            page0reaction2text.SetActive(false);

            page1reaction1text.SetActive(true);
            page1reaction1button.SetActive(true);
            page1reaction2text.SetActive(true);
            page1reaction2button.SetActive(true);

            forwardButton.SetActive(false);
        }
        else if (index == 1)
        {
            titlu2.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
            TextMeshProUGUI textMeshPro = titlu2.GetComponent<TextMeshProUGUI>();
            if (textMeshPro != null)
            {
                textMeshPro.text = "Redox Reactions";
            }

            page2reaction1button.SetActive(false);
            page2reaction1text.SetActive(false);
            page2reaction2button.SetActive(false);
            page2reaction2text.SetActive(false);
        }
        else if (index == 2)
        {
            titlu3.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
            TextMeshProUGUI textMeshPro = titlu3.GetComponent<TextMeshProUGUI>();
            if (textMeshPro != null)
            {
                textMeshPro.text = "Precipitation Reactions";
            }
        }
    }

    public void RotateBack()
    {
        if (rotate == true) { return; }
        float angle = 0;
        pages[index].SetAsLastSibling();
        BackButtonActions();
        StartCoroutine(Rotate(angle, false));
    }

    public void BackButtonActions()
    {
        if (forwardButton.activeInHierarchy == false)
        {
            forwardButton.SetActive(true);
        }

        if (index - 1 == -1)
        {
            backButton.SetActive(false);
        }

        if (index == 0)
        {
            titlu1.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
            TextMeshProUGUI textMeshPro = titlu1.GetComponent<TextMeshProUGUI>();
            if (textMeshPro != null)
            {
                textMeshPro.text = "Decomposition Reactions";
            }

            page1reaction1text.SetActive(false);
            page1reaction1button.SetActive(false);
            page1reaction2text.SetActive(false);
            page1reaction2button.SetActive(false);

            page0reaction1button.SetActive(true);
            page0reaction2button.SetActive(true);
            page0reaction1text.SetActive(true);
            page0reaction2text.SetActive(true);
        }
        else if (index == 1)
        {
            titlu2.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
            TextMeshProUGUI textMeshPro = titlu2.GetComponent<TextMeshProUGUI>();
            if (textMeshPro != null)
            {
                textMeshPro.text = "Neutralization Reactions";
            }

            page2reaction1button.SetActive(true);
            page2reaction1text.SetActive(true);
            page2reaction2button.SetActive(true);
            page2reaction2text.SetActive(true);
        }
        else if (index == 2)
        {
            titlu3.transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
            TextMeshProUGUI textMeshPro = titlu3.GetComponent<TextMeshProUGUI>();
            if (textMeshPro != null)
            {
                textMeshPro.text = "Combustion Reactions";
            }
        }
    }

    IEnumerator Rotate(float angle, bool forward)
    {
        float value = 0f;
        while (true)
        {
            rotate = true;
            Quaternion targetRotation = Quaternion.Euler(0, angle, 0);
            value += Time.deltaTime * pageSpeed;
            pages[index].rotation = Quaternion.Slerp(pages[index].rotation, targetRotation, value);
            float angle1 = Quaternion.Angle(pages[index].rotation, targetRotation);
            if (angle1 < 0.1f)
            {
                if (forward == false)
                {
                    index--;
                }

                rotate = false;
                break;
            }

            yield return null;
        }
    }

    public void Reaction1()
    {
        if (!TryOpenReactionLearningFlow(1))
        {
            StartReactionExperiment(1);
        }
    }

    public void Reaction2()
    {
        if (!TryOpenReactionLearningFlow(2))
        {
            StartReactionExperiment(2);
        }
    }

    public void Reaction3()
    {
        if (!TryOpenReactionLearningFlow(3))
        {
            StartReactionExperiment(3);
        }
    }

    public void Reaction4()
    {
        if (!TryOpenReactionLearningFlow(4))
        {
            StartReactionExperiment(4);
        }
    }

    public void Reaction5()
    {
        if (!TryOpenReactionLearningFlow(5))
        {
            StartReactionExperiment(5);
        }
    }

    public void Reaction6()
    {
        if (!TryOpenReactionLearningFlow(6))
        {
            StartReactionExperiment(6);
        }
    }

    public void Reaction7()
    {
        if (!TryOpenReactionLearningFlow(7))
        {
            StartReactionExperiment(7);
        }
    }

    public void Reaction8()
    {
        if (!TryOpenReactionLearningFlow(8))
        {
            StartReactionExperiment(8);
        }
    }
}
