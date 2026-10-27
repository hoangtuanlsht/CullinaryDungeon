using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private const string FileName = "save.json";

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    private static string TempPath => FilePath + ".tmp";
    private static string BackupPath => FilePath + ".bak";

    public static bool HasSave() => File.Exists(FilePath) || File.Exists(BackupPath);

    /// Ghi an toàn: ghi file tạm → thay file thật, file thật cũ thành backup.
    /// Tắt game giữa chừng thì hoặc còn file cũ, hoặc đã có file mới; không bao giờ có file ghi dở.
    public static bool Save(SaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(TempPath, json);

            if (File.Exists(FilePath)) File.Replace(TempPath, FilePath, BackupPath);
            else File.Move(TempPath, FilePath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Lưu game thất bại: {e.Message}");
            return false;
        }
    }
    /// Đọc file chính; hỏng thì thử backup; cả hai hỏng/không có thì trả false.
    public static bool TryLoad(out SaveData data)
    {
        if(TryRead(FilePath, out data)) return true;
        if (File.Exists(FilePath)) Debug.LogWarning("save.json hỏng, thử đọc bản backup");
        if(TryRead(BackupPath, out data)) return true;
        if (File.Exists(BackupPath)) Debug.LogWarning("save.json.bak cũng hỏng: coi như không có save");
        return false;
    }
    private static bool TryRead(string path, out SaveData data)
    {
        data = null;
        if(!File.Exists(path)) return false;
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return false;
            data = JsonUtility.FromJson<SaveData>(json);
            return data != null && data.version > 0;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Không đọc được {Path.GetFileName(path)}: {e.Message}");
            data = null;
            return false;
        }
    }
    public static void DeleteSave()
    {
        foreach(string p in new[] {FilePath, BackupPath, TempPath})
            if(File.Exists(p)) File.Delete(p);
    }
}
