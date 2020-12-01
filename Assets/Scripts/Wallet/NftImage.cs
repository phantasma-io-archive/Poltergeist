using UnityEngine;
using UnityEngine.Networking;
using Phantasma.SDK;
using System.Collections;

// Storing NFT images.
public static class NftImages
{
    public static void Clear()
    {
        Images.Clear();
    }

    public struct Image
    {
        public string Url;
        public Texture2D Texture;
    }

    private static Hashtable Images = new Hashtable();

    public static bool CheckIfImageLoaded(string Url)
    {
        return Images.Contains(Url);
    }

    public static Image GetImage(string Url)
    {
        return Images.Contains(Url) ? (Image)Images[Url] : new Image();
    }

    private static int imagesLoadedSimultaneously = 0;

    public static IEnumerator DownloadImage(string symbol, string url, string nftId)
    {
        // Trying to avoid downloading same image multiple times.
        if (CheckIfImageLoaded(url))
        {
            yield break;
        }

        var texture = Cache.GetTexture($"{symbol.ToLower()}-image-{nftId}", 0);
        if (texture != null)
        {
            var image = new Image();
            image.Url = url;
            image.Texture = texture;

            lock (Images)
            {
                if (!CheckIfImageLoaded(image.Url))
                    Images.Add(image.Url, image);
            }
            yield break;
        }

        while (imagesLoadedSimultaneously > 5)
        {
            yield return null;

            // Trying to avoid downloading same image multiple times.
            if (CheckIfImageLoaded(url))
            {
                yield break;
            }
        }

        imagesLoadedSimultaneously++;

        var fullUrl = url;
        if(!fullUrl.Contains("/"))
        {
            // This is an IPFS hash.
            fullUrl = "https://gateway.ipfs.io/ipfs/" + fullUrl;
        }

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl);
        yield return request.SendWebRequest();
        if (request.isNetworkError || request.isHttpError)
            Log.Write(request.error);
        else
        {
            var image = new Image();
            image.Url = url;
            image.Texture = ((DownloadHandlerTexture)request.downloadHandler).texture;

            lock (Images)
            {
                if (!CheckIfImageLoaded(image.Url))
                    Images.Add(image.Url, image);
            }

            Cache.AddTexture($"{symbol.ToLower()}-image-{nftId}", image.Texture);
        }
        imagesLoadedSimultaneously--;
    }
}
