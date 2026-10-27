using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// [Module 10] Đặt trong PersistentGameplay. Nạp save (hoặc New Game) TRƯỚC, rồi mới load tầng,
/// để mọi Start() của tầng (AbilityPickup, BreakableFloor, Chest…) đọc WorldProgress đã đúng.

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private ScreenFader fader;
    [SerializeField] private float fadeDuration = 0.25f;

    [Header("New game")]
    [SerializeField] private SceneField newGameScene;// chép từ ô Level Scene của MainMenuManeger
    [SerializeField] private string newGameSpawnPoint; // chép từ ô Spawn Point của MainMenuManeger
    private IEnumerator Start()
    {
        SaveManager saves = SaveManager.Instance;
        saves.IsLoading = true;
        player.BeginTransition(); // không rơi, không nhận input trong lúc nạp
        StartCoroutine(fader.FadeTo(1f, 0f));// đen ngay lập tức

        yield return null;// chờ mọi Start() trong PersistentGameplay (InventoryManager…)

        bool continueGame = SceneData.ContinueRequested;
        SceneData.ContinueRequested = false;

        SaveData data = null;
        if(continueGame && !(SaveSystem.TryLoad(out data) && SaveMigrator.TryUpgrade(data)))
        {
            Debug.LogWarning("Không nạp được save: bắt đầu game mới");
            continueGame = false;
        }

        string sceneName = newGameScene;
        string checkpointId = null;
        if(continueGame)
        {
            saves.Restore(data);
            sceneName = data.player.sceneName;
            checkpointId = data.player.checkpointId;
        }
        else
        {
            saves.ResetForNewGame();
        }
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"Scene '{sceneName}' không có trong Build Settings: về scene đầu");
            sceneName = newGameScene;
            checkpointId = null;
        }

        SceneData.SpawnPointName = newGameSpawnPoint;           // SpawnManager (nếu có) dùng làm điểm mặc định
        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        Scene scene = SceneManager.GetSceneByName(sceneName);
        SceneManager.SetActiveScene(scene);
        
        Vector3 position = player.transform.position;// nơi SpawnManager vừa đặt (nếu có)
        CheckPoint cp = FindCheckpoint(scene,checkpointId);
        if (cp != null)
        {
            position = cp.transform.position;
            player.RestoreSavePoint(position,cp.CheckPointId,sceneName);
        }
        else if (continueGame)
        {
            Debug.LogWarning($"Không có checkpoint nào trong '{sceneName}': đứng tại điểm mặc định");

        }
        player.EndTransition(position, ExitDirection.Right, Vector2.zero);

        yield return new WaitForFixedUpdate();                  // RoomTracker bật vcam của phòng
        yield return null;                                      // Cinemachine đặt camera
        saves.IsLoading = false;
        yield return fader.FadeTo(0f, fadeDuration);
    }
    /// Đúng ID → checkpoint đó. Không thấy (đã xoá khỏi map) → checkpoint đầu tiên của tầng.
    private static CheckPoint FindCheckpoint(Scene scene, string id)
    {
        CheckPoint first = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (CheckPoint c in root.GetComponentsInChildren<CheckPoint>(true))
            {
                if (!string.IsNullOrEmpty(id) && c.CheckPointId == id) return c;
                if(first == null) first = c;
            }
        }
        if (!string.IsNullOrEmpty(id) && first != null)
            Debug.LogWarning($"Không tìm thấy checkpoint '{id}': dùng '{first.CheckPointId}'");
        return string.IsNullOrEmpty(id) ? null : first;
    }

}
