using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManeger : MonoBehaviour
{
    [SerializeField] private GameObject loadingBarObject;
    [SerializeField] private Image loadingBar ;
    [SerializeField] private GameObject[] objectsToHide;

    [SerializeField] private SceneField persistentGameplay;

    [Header("Module 10")]
    [SerializeField] private Button continueButton;
    [SerializeField] private GameObject confirmNewGamePanel;  // "Xoá save cũ và chơi lại?" + 2 nút

    void Awake()
    {
        loadingBarObject.SetActive(false);
        if(confirmNewGamePanel != null) confirmNewGamePanel.SetActive(false);
        if (continueButton != null) continueButton.interactable = SaveMigrator.CanContinue();
    }

    public void ContinueGame()
    {
        SceneData.ContinueRequested = true;
        StartLoading();
    }
    public void NewGame()
    {
        if (SaveSystem.HasSave() && confirmNewGamePanel != null) confirmNewGamePanel.SetActive(true);
        else ConfirmNewGame(); 
    }
    // Nút "Đồng ý" trong panel xác nhận
    public void ConfirmNewGame()
    {
        SaveSystem.DeleteSave();
        SceneData.ContinueRequested=false;
        StartLoading();
    }
    public void CancelNewGame() => confirmNewGamePanel.SetActive(false);

    public void QuitMenu() => Application.Quit();
    private void StartLoading()
    {
        foreach(GameObject go in objectsToHide) go.SetActive(false);
        if(confirmNewGamePanel != null) confirmNewGamePanel.SetActive(false);
        loadingBarObject.SetActive(true);
        StartCoroutine(Load());
    }
    private IEnumerator Load()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(persistentGameplay);// Single: MainMenu bị unload
        while (!op.isDone)
        {
            loadingBar.fillAmount = op.progress / 0.9f;  // progress dừng ở 0.9 cho tới lúc kích hoạt
            yield return null;
        }
    }

}
