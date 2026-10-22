using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// [Module 10] Dữ liệu ghi ra file. CHỈ được chứa: int, float, bool, string, List/mảng,
/// và class [Serializable] khác cũng chỉ chứa những thứ đó. Không asset, không GameObject.
///
/// Quy ước ID (không bao giờ đổi một ID đã phát hành, kể cả khi đổi tên hiển thị):
///   Phòng       F<tầng>_R<số>         vd F1_R03       (Room.roomId, có từ Module 5)
///   Checkpoint  F<tầng>_CP<số>        vd F2_CP01
///   Rương       F<tầng>_Chest<số>     vd F3_Chest02
///   Boss        boss_F<tầng>_<tên>    vd boss_F1_Slime
///   Sàn vỡ      F<tầng>_R<số>_Floor   (BreakableFloor.floorId, có từ Module 7)
///   Item        item_<tên_thường>     vd item_monster_meat
//SaveData
[Serializable]
public class SaveData
{
    public int version;
    public string savedAtUtc;
    public PlayerSave player = new();
    public WorldSave world = new();
    public InventorySave inventory = new();
}
//PlayerSave
[Serializable]
public class PlayerSave
{
    public float damage;
    public int coin;
    public string abilities;
    public float hunger;
    public string sceneName;
    public string checkpointId;
}
//WorldSave
[Serializable]
public class WorldSave
{
    public List<string> visitedRooms = new();
    public List<string> flags  = new();
}
//InventorySave
[Serializable]
public class InventorySave
{
    public List <SlotSave> slots = new();
    public List <SlotSave> loose = new();// do tren chuot
}
//SlotSave
[Serializable]
public class SlotSave
{
    public int index;
    public string itemId;
    public int quantity;
}
