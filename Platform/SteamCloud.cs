using System.IO;
using System.Text;
using Steamworks;
using UnityEngine;

// Steam Cloud through the Remote Storage API (not Auto-Cloud): the save (save.json) and the achievement record
// (achievements.json) are mirrored to the player's cloud every time they are written, and a cloud copy newer than
// the one on disk (another PC, a reinstall) replaces it when the save is read. The files on disk stay the source:
// without Steam, or with the cloud off for the account or the app, everything works locally and nothing is lost.
// Steamworks needs the quota set for the app (App Admin > Cloud: 10 MB and 10 files is plenty; both files together
// are under 10 KB). Valve lets a demo write to the full game's cloud so a buyer goes on where the demo stopped, but
// only once the full game is released (doc/store/application/demos); until then the demo has its own quota.
public static class SteamCloud
{
    // Seconds a cloud copy must be newer than the file on disk to replace it (file times and Steam's clock differ).
    private const long NewerBySeconds = 2;

    public static bool Available => SteamPlatform.IsReady && SteamRemoteStorage.IsCloudEnabledForAccount() && SteamRemoteStorage.IsCloudEnabledForApp();

    // Mirrors the file on disk to the cloud under cloudName.
    public static bool Push(string cloudName, string localPath)
    {
        if (!Available || !File.Exists(localPath))
        {
            return false;
        }

        try
        {
            byte[] data = File.ReadAllBytes(localPath);
            bool ok = SteamRemoteStorage.FileWrite(cloudName, data, data.Length);
            if (!ok)
            {
                Debug.LogWarning("[Steam Cloud] não gravou " + cloudName + " (" + data.Length + " bytes; cota do app configurada?).");
            }

            return ok;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Steam Cloud] " + cloudName + ": " + e.Message);
            return false;
        }
    }

    // When the cloud copy is newer than the file on disk (or the file is missing), writes it to disk with the
    // cloud's time, so the next read does not see it as newer again. True when the file was replaced.
    public static bool PullIfNewer(string cloudName, string localPath)
    {
        if (!Available || !SteamRemoteStorage.FileExists(cloudName))
        {
            return false;
        }

        long cloudTime = SteamRemoteStorage.GetFileTimestamp(cloudName);
        long localTime = File.Exists(localPath) ? new System.DateTimeOffset(File.GetLastWriteTimeUtc(localPath)).ToUnixTimeSeconds() : 0;
        if (cloudTime <= localTime + NewerBySeconds)
        {
            return false;
        }

        byte[] data = Read(cloudName);
        if (data == null)
        {
            return false;
        }

        try
        {
            File.WriteAllBytes(localPath, data);
            File.SetLastWriteTimeUtc(localPath, System.DateTimeOffset.FromUnixTimeSeconds(cloudTime).UtcDateTime);
            Debug.Log("[Steam Cloud] " + cloudName + " da nuvem é mais novo: trazido para " + localPath + ".");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Steam Cloud] " + cloudName + ": " + e.Message);
            return false;
        }
    }

    // The cloud copy as text, or null.
    public static string ReadText(string cloudName)
    {
        if (!Available || !SteamRemoteStorage.FileExists(cloudName))
        {
            return null;
        }

        byte[] data = Read(cloudName);
        return data != null ? Encoding.UTF8.GetString(data) : null;
    }

    public static void Delete(string cloudName)
    {
        if (Available && SteamRemoteStorage.FileExists(cloudName))
        {
            SteamRemoteStorage.FileDelete(cloudName);
        }
    }

    private static byte[] Read(string cloudName)
    {
        int size = SteamRemoteStorage.GetFileSize(cloudName);
        if (size <= 0)
        {
            return null;
        }

        byte[] data = new byte[size];
        int read = SteamRemoteStorage.FileRead(cloudName, data, size);
        return read == size ? data : null;
    }
}
