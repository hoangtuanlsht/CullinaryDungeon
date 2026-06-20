using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class NPC : MonoBehaviour, Interact
{
    public NPCDialogs dialogData;
    public GameObject dialogPanel;
    public TMP_Text dialogText, nameText;
    public Image portraitImage;

    private int dialogIndex;
    private bool isTyping, isDialogActive;

    

    public bool CanInteract()
    {
        return !isDialogActive;
    }
    public void InteractWithObject()
    {
        if(dialogData == null || (PauseController.IsGamePause && !isDialogActive)) return;

        if (isDialogActive)
        {
            NextLine();
        }
        else
        {
            StartDialog();
        }
    }
    void StartDialog()
    {
        isDialogActive = true; ;
        dialogIndex = 0;

        nameText.SetText(dialogData.name);
        portraitImage.sprite = dialogData.npcPortrait;

        dialogPanel.SetActive(true);
        PauseController.SetPause(true);

        StartCoroutine(TypeLine());
    }
    void NextLine()
    {
        if (isTyping)
        {
            StopAllCoroutines();
            dialogText.SetText(dialogData.dialogueLines[dialogIndex]);
            isTyping = false;
        }
        else if (++dialogIndex < dialogData.dialogueLines.Length)
        {
            StartCoroutine(TypeLine());
        }
        else
        {
            EndDialog();
        }
    }
    IEnumerator TypeLine()
    {
        isTyping = true;
        dialogText.SetText("");

        foreach(char letter in dialogData.dialogueLines[dialogIndex])
        {
            dialogText.text += letter;
            SoundManager.Play("SpeechSound");
            yield return new WaitForSecondsRealtime(dialogData.typingSpeed);
        }
        isTyping = false;

        if(dialogData.autoProgressLines.Length > dialogIndex && dialogData.autoProgressLines[dialogIndex])
        {
            yield return new WaitForSecondsRealtime(dialogData.autoProgressDelay);
            NextLine();
        }
    }

    public void EndDialog()
    {
        StopAllCoroutines();
        isDialogActive = false;
        dialogText.SetText("");
        dialogPanel.SetActive(false);
        PauseController.SetPause(false);

    }
}
