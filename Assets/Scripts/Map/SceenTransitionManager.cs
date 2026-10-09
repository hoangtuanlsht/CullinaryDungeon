using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// [Module 5] Sống trong PersistentGameplay (không bao giờ unload) → coroutine không bị cắt ngang.
public class SceneTransitionManager : Singleton<SceneTransitionManager>
{
    [SerializeField] private Player player;
    [SerializeField] private ScreenFader fader;
    [SerializeField] private float fadeDuration = 0.25f;

    public bool IsBusy { get; private set; }

    public void TravelTo(string targetScene, string targetDoorId, Scene fromScene)
    {
        if (IsBusy) return;                 // chặn chạm cửa 2 lần
        StartCoroutine(TravelRoutine(targetScene, targetDoorId, fromScene));
    }

    private IEnumerator TravelRoutine(string targetScene, string targetDoorId, Scene fromScene)
    {
        IsBusy = true;
        Vector2 velocityBefore = player.rb.velocity;
        player.BeginTransition();

        yield return fader.FadeTo(1f, fadeDuration);

        // 1) Unload tầng cũ TRƯỚC: các tầng có thể dùng chung vùng tọa độ, load trước thì 2 tầng chồng lên nhau.
        if (fromScene.IsValid() && fromScene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(fromScene);

        // 2) Load tầng mới và đặt làm active scene
        //    (để Instantiate — đồ rơi, Flame... — rơi vào scene tầng và bị dọn theo khi unload).
        yield return SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
        Scene newScene = SceneManager.GetSceneByName(targetScene);
        SceneManager.SetActiveScene(newScene);

        // 3) Tìm cửa đích theo ID — chỉ trong scene vừa load
        SceneDoor door = FindDoor(newScene, targetDoorId);
        if (door == null)
        {
            Debug.LogError($"Không tìm thấy cửa '{targetDoorId}' trong scene '{targetScene}'");
            player.EndTransition(player.transform.position, ExitDirection.Right, Vector2.zero);
        }
        else
        {
            player.EndTransition(door.SpawnPoint.position, door.EnterDirection, velocityBefore);
        }

        // 4) Chờ RoomTracker (FixedUpdate) bật vcam phòng mới + 1 frame cho Cinemachine đặt camera
        yield return new WaitForFixedUpdate();
        yield return null;

        yield return fader.FadeTo(0f, fadeDuration);
        IsBusy = false;
    }

    private static SceneDoor FindDoor(Scene scene, string id)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (SceneDoor d in root.GetComponentsInChildren<SceneDoor>(true))
                if (d.DoorId == id) return d;
        return null;
    }
}
