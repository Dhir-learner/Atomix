using TMPro;
using UnityEngine;

public class BookCanvasManager : MonoBehaviour
{
    public GameObject bookCanvas;
    public TMP_Text canvasText;
    private bool firstOpening;
    public AudioSource audioSource;
    public AudioClip clip_start;
    public AudioClip clip;

    void Start()
    {
        bookCanvas.SetActive(false);
        audioSource.PlayOneShot(clip_start);
        firstOpening = true;
    }

    public void openTheBook()
    {
        bookCanvas.SetActive(true);
        FirstPersonController.SetCursorLock(IsDesktopBookMode());
        Debug.Log("onActivate");

        if (firstOpening)
        {
            canvasText.text = "Now you can search through the book for the desired reaction by clicking on the Next and Previous buttons. When you have decided, click on the Play button.";
            audioSource.Stop();
            audioSource.PlayOneShot(clip);
            firstOpening = false;
        }
    }

    public void CloseBook()
    {
        bookCanvas.SetActive(false);
        FirstPersonController.SetCursorLock(true);
    }

    bool IsDesktopBookMode()
    {
        FirstPersonController[] desktopControllers = FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return desktopControllers != null && desktopControllers.Length > 0;
    }

    public void onFirstHoverEntered()
    {
        Debug.Log("onFirstHoverEntered");
    }

    public void onHoverEntered()
    {
        Debug.Log("onHoverEntered");
    }

    public void onHoverExited()
    {
        Debug.Log("onHoverExited");
    }

    public void onLastHoverExited()
    {
        Debug.Log("onLastHoverExited");
    }

    public void onSelectEntered()
    {
        Debug.Log("onSelectEntered");
    }

    public void onSelectExited()
    {
        Debug.Log("onSelectExited");
    }

    public void onSelectCanceled()
    {
        Debug.Log("onSelectCanceled");
    }

    public void onDeactivate()
    {
        Debug.Log("onDeactivate");
    }
}
