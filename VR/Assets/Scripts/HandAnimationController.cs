using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls hand animations for keyboard and mouse gameplay
/// Trigger animation = Left Mouse Button
/// Grip animation = Right Mouse Button or G key
/// </summary>
public class HandAnimationController : MonoBehaviour
{
    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"No Animator component found on {gameObject.name}!");
        }
    }

    void Update()
    {
        if (animator == null) return;

        // Trigger value from left mouse button (0 or 1)
        float triggerValue = Input.GetMouseButton(0) ? 1f : 0f;

        // Grip value from right mouse button or G key (0 or 1)
        float gripValue = (Input.GetMouseButton(1) || Input.GetKey(KeyCode.G)) ? 1f : 0f;

        animator.SetFloat("Trigger", triggerValue);
        animator.SetFloat("Grip", gripValue);
    }
}
