using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadManager : Interact
{
    [SerializeField] private string spawnPoint;
    [SerializeField] private SceneField[] scenesToLoad;
    [SerializeField] private SceneField[] scenesToUnLoad;

    private GameObject player;
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
    }

    public override void InteractWithObject()
    {
        SceneData.SpawnPointName = spawnPoint;
        LoadScene();
        UnLoadScene();
    }
    private void LoadScene()
    {
        for (int i = 0; i < scenesToLoad.Length; i++) 
        { 
            bool isSceneLoaded = false;
            for(int j = 0; j < SceneManager.sceneCount; j++)
            {
                Scene loadScene = SceneManager.GetSceneAt(j);
                if (loadScene.name == scenesToLoad[i].SceneName)
                {
                    isSceneLoaded = true;
                    break;
                }
            }
            if (!isSceneLoaded) 
            {
                //StartCoroutine(LoadSceneCoroutine());
                SceneManager.LoadSceneAsync(scenesToLoad[i], LoadSceneMode.Additive);
            }
        }
    }
    private void UnLoadScene()
    {
        for (int i = 0; i < scenesToUnLoad.Length; i++) 
        {
            for(int j = 0;j < SceneManager.sceneCount; j++)
            {
                Scene loadScene = SceneManager.GetSceneAt(j);
                if(loadScene.name == scenesToUnLoad[i].SceneName)
                {
                    SceneManager.UnloadSceneAsync(scenesToUnLoad[i]);
                }
            }
        }
    }
    
}
