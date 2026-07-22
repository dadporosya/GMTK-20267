using NUnit.Framework;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class Monologue : MonoBehaviour
{
    [SerializeField] GameObject monologueFrame;
    [SerializeField] bool disableControlsOnMonologue;
    private Movement playerMovement;
    private MouseLook playerMouseLook;
    private TMP_Text monologueTextObj;
    private TypewriterText monologueTWObj;
    private int currentPage;
    private int pagesCount;
    private string[] currentTextPages;

    [HideInInspector] public bool isMonologuing = false;


    private void Awake()
    {
        monologueTextObj = monologueFrame.GetComponentInChildren<TMP_Text>();
        monologueTWObj = monologueTextObj.GetComponent<TypewriterText>();
        playerMovement = GetComponent<Movement>();
        playerMouseLook = GetComponent<MouseLook>();
    }

    private void Start()
    {
        isMonologuing = false;
    }

    public void SetTextObj(TypewriterText newTextObj)
    {
        monologueFrame = newTextObj.gameObject;
        monologueTextObj = monologueFrame.GetComponentInChildren<TMP_Text>();
        monologueTWObj = monologueTextObj.GetComponent<TypewriterText>();
    }


    public void StopMonologuing()
    {

        monologueFrame.SetActive(false);
        isMonologuing = false;
        if (playerMouseLook != null && playerMovement != null)
        {
            if (disableControlsOnMonologue)
            {
                playerMovement.canMove = true;
                playerMouseLook.canLook = true;
            }

        }

        currentTextPages = null;
    }

    public void StopMonologuingAfterDelay(float delay)
    {
        StartCoroutine(MonologueStopWithDelayCoroutine(delay));
    }

    private IEnumerator MonologueStopWithDelayCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        StopMonologuing();
    }



    public void MonologueText(string[] textPages)
    {
        if (!isMonologuing)
        {
            if (monologueTextObj.gameObject.activeSelf == false || monologueTextObj == null)
            {
                monologueTextObj = monologueFrame.GetComponentInChildren<TMP_Text>();
                monologueTWObj = monologueTextObj.GetComponent<TypewriterText>();
            }
            currentTextPages = textPages;
            pagesCount = currentTextPages.Length;
            currentPage = 0;
            if (playerMouseLook != null && playerMovement != null)
            {
                if (disableControlsOnMonologue)
                {
                    playerMovement.canMove = false;
                    playerMouseLook.canLook = false;
                }


            }
            isMonologuing = true;
            monologueFrame.SetActive(true);
            monologueTWObj.SetText(currentTextPages[currentPage]);
            currentPage++;
        }
        else
        {
            List<string> textPagesList = currentTextPages.ToList<string>();
            textPagesList.AddRange(textPages);
            currentTextPages = textPagesList.ToArray();
            pagesCount = currentTextPages.Length;
        }


    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (monologueTextObj.maxVisibleCharacters >= monologueTextObj.textInfo.characterCount && monologueFrame.activeSelf)
            {
                if (currentPage < pagesCount)
                {
                    monologueTWObj.SetText(currentTextPages[currentPage]);
                    currentPage++;
                }
                else
                {
                    monologueFrame.SetActive(false);
                    isMonologuing = false;
                    if (playerMouseLook != null && playerMovement != null)
                    {
                        if (disableControlsOnMonologue)
                        {
                            playerMovement.canMove = true;
                            playerMouseLook.canLook = true;
                        }


                    }

                }


            }

        }
    }


}
