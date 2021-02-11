using System;
using System.IO;
using LunarLabs.Parser;
using UnityEngine;
using Phantasma.SDK;

public static class Cache
{
    public enum FileType
    {
        JSON,
        PNG
    }
    private static string FolderPath;
    private static string ImageFolderPath;
    private static string FilePath;
    private static bool ForceCacheUsage = false;

    public static void Init(string folderName, bool forceCacheUsage = false)
    {
        FolderPath = Path.Combine(Application.persistentDataPath, folderName);
        if (!Directory.Exists(FolderPath))
            Directory.CreateDirectory(FolderPath);

        ImageFolderPath = Path.Combine(FolderPath, "image");
        if (!Directory.Exists(ImageFolderPath))
            Directory.CreateDirectory(ImageFolderPath);

        FilePath = Path.Combine(FolderPath, "cache.json");
        ForceCacheUsage = forceCacheUsage;
    }

    public static void Clear()
    {
        System.IO.DirectoryInfo directoryInfo = new DirectoryInfo(FolderPath);

        foreach (FileInfo file in directoryInfo.GetFiles())
        {
            file.Delete();
        }
        foreach (DirectoryInfo dir in directoryInfo.GetDirectories())
        {
            dir.Delete(true);
        }

        Directory.CreateDirectory(ImageFolderPath);
    }

    private static void UpdateRegistry(string CacheId, DateTime Timestamp, int Size, string WalletName)
    {
        DataNode root;

        if (File.Exists(FilePath))
        {
            root = DataFormats.LoadFromFile(FilePath);
        }
        else
        {
            root = DataNode.CreateObject();
        }

        DataNode caches;
        if (root.HasNode("caches"))
            caches = root.GetNode("caches");
        else
            caches = root.AddNode(DataNode.CreateObject("caches"));

        DataNode cacheNode;
        if (caches.HasNode(CacheId))
        {
            cacheNode = caches.GetNode(CacheId);
        }
        else
        {
            cacheNode = caches.AddNode(DataNode.CreateObject(CacheId));
        }

        if (cacheNode.HasNode("timestamp"))
            cacheNode.GetNode("timestamp").Value = Timestamp.ToString();
        else
            cacheNode.AddField("timestamp", Timestamp.ToString());

        if (cacheNode.HasNode("size"))
            cacheNode.GetNode("size").Value = Size.ToString();
        else
            cacheNode.AddField("size", Size.ToString());

        if (cacheNode.HasNode("wallet-name"))
            cacheNode.GetNode("wallet-name").Value = WalletName;
        else
        {
            if(!String.IsNullOrEmpty(WalletName))
                cacheNode.AddField("wallet-name", WalletName);
        }

        DataFormats.SaveToFile(FilePath, root);
    }

    private static Nullable<DateTime> GetRegistryTimestamp(string CacheId)
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        var root = DataFormats.LoadFromFile(FilePath);

        var caches = root.GetNode("caches");
        if (caches == null)
        {
            Log.Write("caches == null");
            return null;
        }

        var cacheNode = caches.GetNode(CacheId);
        if (cacheNode == null)
        {
            return null;
        }

        string timestamp = cacheNode.GetString("timestamp");

        if (String.IsNullOrEmpty(timestamp))
        {
            return null;
        }

        if (DateTime.TryParse(timestamp, out var ts))
            return ts;
        else
            return null;
    }

    private static string GetFilePath(string CacheId, FileType FileType)
    {
        if(FileType == FileType.PNG)
            return Path.Combine(ImageFolderPath, "cache." + CacheId + "." + FileType.ToString().ToLower());
        else
            return Path.Combine(FolderPath, "cache." + CacheId + "." + FileType.ToString().ToLower());
    }

    private static string GetFilePathIfCacheIsValid(string CacheId, FileType FileType, int CacheLifetimeInMinutes, string WalletAddress = "")
    {
        if (!String.IsNullOrEmpty(WalletAddress))
            CacheId = CacheId + "." + WalletAddress;

        string filePath = GetFilePath(CacheId, FileType);

        if (!ForceCacheUsage && CacheLifetimeInMinutes > 0)
        {
            Nullable<DateTime> _timestamp = GetRegistryTimestamp(CacheId);

            if (_timestamp == null)
            {
                return null;
            }

            DateTime _timestamp_nn = (DateTime)_timestamp;

            if (_timestamp_nn.AddMinutes(CacheLifetimeInMinutes) < DateTime.UtcNow)
            {
                // Cash is outdated.
                return null;
            }
        }

        if (!File.Exists(filePath))
        {
            return null;
        }

        return filePath;
    }

    public static void Add(string CacheId, FileType FileType, string CacheContents, string WalletAddress = "", string WalletName = "")
    {
        if (!String.IsNullOrEmpty(WalletAddress))
            CacheId = CacheId + "." + WalletAddress;

        string filePath = GetFilePath(CacheId, FileType);

        File.WriteAllText(filePath, CacheContents);

        UpdateRegistry(CacheId, DateTime.Now, System.Text.ASCIIEncoding.ASCII.GetByteCount(CacheContents), WalletName);
    }

    public static void AddTexture(string CacheId, Texture2D CacheContents, string WalletAddress = "", string WalletName = "")
    {
        if (!String.IsNullOrEmpty(WalletAddress))
            CacheId = CacheId + "." + WalletAddress;

        string filePath = GetFilePath(CacheId, FileType.PNG);

        var imageSizeLimit = 256;
        if (CacheContents.width > imageSizeLimit)
        {
            TextureScaler.scale(CacheContents, imageSizeLimit, (int)((double)imageSizeLimit / CacheContents.width * CacheContents.height));
        }
        else if (CacheContents.height > imageSizeLimit)
        {
            TextureScaler.scale(CacheContents, (int)((double)imageSizeLimit / CacheContents.height * CacheContents.width), imageSizeLimit);
        }

        byte[] bytes = CacheContents.EncodeToPNG();
        File.WriteAllBytes(filePath, bytes);

        UpdateRegistry(CacheId, DateTime.Now, bytes.Length, WalletName);
    }
    public static void ClearTexture(string CacheId, string WalletAddress = "", string WalletName = "")
    {
        if (!String.IsNullOrEmpty(WalletAddress))
            CacheId = CacheId + "." + WalletAddress;

        string filePath = GetFilePath(CacheId, FileType.PNG);

        File.Delete(filePath);

        UpdateRegistry(CacheId, DateTime.Now, 0, WalletName);
    }

    public static void AddDataNode(string CacheId, FileType FileType, DataNode CacheContents, string WalletAddress = "", string WalletName = "")
    {
        if (!String.IsNullOrEmpty(WalletAddress))
            CacheId = CacheId + "." + WalletAddress;

        string filePath = GetFilePath(CacheId, FileType);

        var serializedCacheContents = DataFormats.SaveToString(DataFormat.JSON, CacheContents);
        File.WriteAllText(filePath, serializedCacheContents);

        UpdateRegistry(CacheId, DateTime.Now, System.Text.ASCIIEncoding.ASCII.GetByteCount(serializedCacheContents), WalletName);
    }
    public static void ClearDataNode(string CacheId, FileType FileType, string WalletAddress = "", string WalletName = "")
    {
        if (!String.IsNullOrEmpty(WalletAddress))
            CacheId = CacheId + "." + WalletAddress;

        string filePath = GetFilePath(CacheId, FileType);

        File.Delete(filePath);

        UpdateRegistry(CacheId, DateTime.Now, 0, WalletName);
    }

    public static string GetAsString(string CacheId, FileType FileType, int CacheLifetimeInMinutes, string WalletAddress = "")
    {
        if (!String.IsNullOrEmpty(WalletAddress))
            CacheId = CacheId + "." + WalletAddress;

        var filePath = GetFilePathIfCacheIsValid(CacheId, FileType, CacheLifetimeInMinutes);

        if (String.IsNullOrEmpty(filePath))
            return null;

        return File.ReadAllText(filePath);
    }

    public static byte[] GetAsByteArray(string CacheId, FileType FileType, int CacheLifetimeInMinutes, string WalletAddress = "")
    {
        if (!String.IsNullOrEmpty(WalletAddress))
            CacheId = CacheId + "." + WalletAddress;

        var filePath = GetFilePathIfCacheIsValid(CacheId, FileType, CacheLifetimeInMinutes);

        if (String.IsNullOrEmpty(filePath))
            return null;

        return File.ReadAllBytes(filePath);
    }

    public static Texture2D GetTexture(string CacheId, int CacheLifetimeInMinutes, string WalletAddress = "")
    {
        var bytes = GetAsByteArray(CacheId, FileType.PNG, CacheLifetimeInMinutes, WalletAddress);

        if (bytes == null)
            return null;

        var texture = new Texture2D(2, 2);
        texture.LoadImage(bytes);
        return texture;
    }

    public static DataNode GetDataNode(string CacheId, FileType FileType, int CacheLifetimeInMinutes, string WalletAddress = "")
    {
        var cacheContents = GetAsString(CacheId, FileType, CacheLifetimeInMinutes, WalletAddress);

        if (String.IsNullOrEmpty(cacheContents))
            return null;

        return DataFormats.LoadFromString(cacheContents);
    }
}
