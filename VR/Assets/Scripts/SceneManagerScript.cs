using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerScript : MonoBehaviour
{
    public void LoadScene(string sceneName)
    {
        // Routed through the fade so scene buttons wired directly in a scene behave the same way
        // as the ones that go through MainMenu.
        SceneTransition.Load(sceneName);
    }
}
