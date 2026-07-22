using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

interface IInteractable_3D
{
    public void Interact();
    public bool IsInteractable();
    public void SetInteractive(bool interactive);
}

public class Interact : MonoBehaviour
{
    private Camera cam;
    [SerializeField] Image interactIcon;
    [SerializeField] float iconFadeTime;
    private IInteractable_3D currentInteractable;
    public float interactRange;

    private bool canInteract = true;
    private bool wasInteractableLastFrame = false;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        cam = Camera.main;
        interactIcon.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!canInteract)
        {
            if (interactIcon.gameObject.activeSelf)
            {
                interactIcon.gameObject.SetActive(false);
            }
            currentInteractable = null;
            return;
        }

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        bool foundInteractable = false;
        IInteractable_3D newInteractable = null;

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            if (hit.transform.gameObject.TryGetComponent(out IInteractable_3D interactable))
            {
                if (interactable.IsInteractable())
                {
                    newInteractable = interactable;
                    foundInteractable = true;
                }
            }
        }

        // Обновляем текущий интерактивный объект
        currentInteractable = newInteractable;

        // Обновляем иконку только если состояние изменилось
        bool shouldShowIcon = foundInteractable && currentInteractable != null;
        if (shouldShowIcon != wasInteractableLastFrame)
        {
            if (shouldShowIcon)
            {
                if (hideCoroutine != null) StopCoroutine(hideCoroutine);
                interactIcon.color = new Color(interactIcon.color.r, interactIcon.color.g, interactIcon.color.b, 1);
                interactIcon.gameObject.SetActive(true);
            }
            else
            {
                hideCoroutine = StartCoroutine(HideIconCoroutine());
            }
            wasInteractableLastFrame = shouldShowIcon;
        }

        // Взаимодействие
        if (Input.GetMouseButtonDown(0) && currentInteractable != null)
        {
            currentInteractable.Interact();
        }
    }

    public void SetInteractAbility(bool interact)
    {
        canInteract = interact;
        if (!canInteract)
        {
            interactIcon.gameObject.SetActive(false);
            wasInteractableLastFrame = false;
            currentInteractable = null;
        }
    }

    private IEnumerator HideIconCoroutine()
    {
        float elapsedTime = 0;
        float progress;
        interactIcon.color = new Color(interactIcon.color.r, interactIcon.color.g, interactIcon.color.b, 1);
        Color startingColor = interactIcon.color;
        Color targetColor = startingColor;
        Color currentColor = startingColor;
        targetColor.a = 0;

        while (elapsedTime < iconFadeTime)
        {
            progress = Mathf.Clamp01(elapsedTime / iconFadeTime);
            currentColor.a = Mathf.Lerp(startingColor.a, targetColor.a, progress);
            interactIcon.color = currentColor;
            elapsedTime += Time.deltaTime;
            yield return null;
        }


        interactIcon.gameObject.SetActive(false);
        interactIcon.color = new Color(interactIcon.color.r, interactIcon.color.g, interactIcon.color.b, 1);
    }
}