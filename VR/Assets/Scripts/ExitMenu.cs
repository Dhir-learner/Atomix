using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitMenu : MonoBehaviour
{
    public GameObject bookCanvas;

    public void ReturnMainMenuScene()
    {
        FirstPersonController.SetCursorLock(false);
        SceneManager.LoadScene("MainMenuScene");
        Debug.Log("Opening MainMenuScene");
    }

    public void closeTheBook()
    {
        if (bookCanvas == null)
        {
            Debug.Log("GameObject not found. Ensure the name is correct and the GameObject exists in the scene.");
            return;
        }

        BookCanvasManager bookManager = bookCanvas.GetComponent<BookCanvasManager>();
        if (bookManager != null)
        {
            bookManager.CloseBook();
        }
        else
        {
            bookCanvas.SetActive(false);
        }
    }
}
