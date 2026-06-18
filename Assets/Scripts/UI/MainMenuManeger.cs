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
    [SerializeField] private SceneField levelScene;
    [SerializeField] private string spawnPoint;

    private List<AsyncOperation> sceneToLoad = new List<AsyncOperation>();
    void Awake()
    {
        loadingBarObject.SetActive(false);
    }

    // Update is called once per frame
    public void StartGame()
    {
        HideMenu();

        loadingBarObject.SetActive(true);
        sceneToLoad.Add(SceneManager.LoadSceneAsync(persistentGameplay));
        sceneToLoad.Add(SceneManager.LoadSceneAsync(levelScene, LoadSceneMode.Additive));
        
        StartCoroutine(ProgessLoadingBar());

    }
    private void HideMenu()
    {
        for(int i =0;i<objectsToHide.Length;i++)
        {
            objectsToHide[i].SetActive(false);
        }
    }

    private IEnumerator ProgessLoadingBar()
    {
        Debug.Log("Loading");
        float loadPregess = 0f;
        for(int i = 0; i < sceneToLoad.Count; i++)
        {
            while (!sceneToLoad[i].isDone)
            {
                Debug.Log("scenetoload");
                SceneData.SpawnPointName = spawnPoint;
                loadPregess += sceneToLoad[i].progress;
                loadingBar.fillAmount = loadPregess / sceneToLoad.Count;
                yield return null;
            }
        }
        

    }
}
