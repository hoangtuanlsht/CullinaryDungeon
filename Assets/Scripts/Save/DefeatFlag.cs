using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Character))]
public class DefeatFlag : MonoBehaviour
{
    [Tooltip("Duy nhất toàn game vd cho boss")]
    [SerializeField] private string flagId;
    public string FlagId => flagId;
    private Character character;
    private void Awake()
    {
        character = GetComponent<Character>();
        character.OnDied += HandleDied;
        if (string.IsNullOrEmpty(flagId)) Debug.LogError($"{name}: chưa đặt Flag Id", this);

    }
    private void OnDestroy()
    {
        if (character != null) character.OnDied -= HandleDied;
    }
    private void HandleDied() =>WorldProgress.SetFlag(flagId);
}
