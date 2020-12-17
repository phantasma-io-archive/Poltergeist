using UnityEngine;
using UnityEngine.Networking;
using Phantasma.SDK;
using System.Collections;
using System.Linq;

// Storing NFT images.
public static class NftImages
{
    public static void Clear(string symbol = "")
    {
        if (string.IsNullOrEmpty(symbol))
        {
            Images.Clear();
        }
        else
        {
            var imagesToRemove = Images.Values.Cast<Image>().Where(x => x.Symbol.ToLower() == symbol.ToLower()).ToArray();
            foreach (var imageToRemove in imagesToRemove)
            {
                Images.Remove(imageToRemove.Url);
                Cache.ClearTexture($"{symbol.ToLower()}-image-{imageToRemove.NftId}");
            }
        }
    }

    public struct Image
    {
        public string Url;
        public Texture2D Texture;
        public string Symbol;
        public string NftId;
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

    public static Texture2D CreateReadable(this Texture2D texture)
    {
        Texture2D output = new Texture2D(texture.width, texture.height);

        RenderTexture _renderTemp = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);

        Graphics.Blit(texture, _renderTemp);

        RenderTexture _renderActive = RenderTexture.active;

        RenderTexture.active = _renderTemp;

        output.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);

        RenderTexture.active = _renderActive;

        RenderTexture.ReleaseTemporary(_renderTemp);
        _renderTemp = null;

        output.Apply();

        return output;
    }

    private static bool[] invalidImageMask = new bool[]
    {
    false, false, false, true, true, false, false, false,
    false, false, false, true, true, false, false, false,
    false, false, false, false, false, false, false, false,
    false, false, false, true, true, false, false, false,
    false, false, false, false, true, true, false, false,
    false, true, true, false, false, true, true, false,
    false, true, true, false, false, true, true, false,
    false, false, true, true, true, true, false, false
    };

    public static bool ValidateLoadedTexture(ref Texture2D texture, bool readLockedImages = false)
    {
        if (texture.width == 8 && texture.height == 8)
        {
            Texture2D evaluateTexture = texture;

            if (!evaluateTexture.isReadable)
            {
                if (!readLockedImages)
                    return false;

                evaluateTexture = evaluateTexture.CreateReadable();
            }

            Color32[] pixels = evaluateTexture.GetPixels32();
            for (int i = 0, iC = pixels.Length; i < iC; i++)
            {
                Color32 pixel = pixels[i];
                if (invalidImageMask[i] != ((pixel.r == 255) && (pixel.g == 0) && (pixel.b == 0)))
                    return true;
            }

            return false;
        }

        return true;
    }

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
            image.Symbol = symbol.ToLower();
            image.NftId = nftId;

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
            // This is a pure IPFS hash.
            fullUrl = "https://gateway.ipfs.io/ipfs/" + fullUrl;
        }
        else if (fullUrl.StartsWith("ipfs://"))
        {
            fullUrl = "https://gateway.ipfs.io/ipfs/" + fullUrl.Substring("ipfs://".Length);
        }
        Log.Write("NFT image loading: Full URL: " + fullUrl);

        UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl);
        yield return request.SendWebRequest();
        if (request.isNetworkError || request.isHttpError)
        {
            Log.Write(request.error);

            var image = new Image();
            image.Url = url;
            image.Texture = null;
            image.Symbol = symbol.ToLower();
            image.NftId = nftId;

            lock (Images)
            {
                if (!CheckIfImageLoaded(image.Url))
                    Images.Add(image.Url, image);
            }
        }
        else
        {
            var image = new Image();
            image.Url = url;
            image.Texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
            image.Symbol = symbol.ToLower();
            image.NftId = nftId;

            if (!ValidateLoadedTexture(ref image.Texture, true))
            {
                Log.Write("NFT image loading: Invalid image.");
                image.Texture = null;
            }

            lock (Images)
            {
                if (!CheckIfImageLoaded(image.Url))
                    Images.Add(image.Url, image);
            }

            if(image.Texture)
                Cache.AddTexture($"{symbol.ToLower()}-image-{nftId}", image.Texture);
        }
        imagesLoadedSimultaneously--;
    }
}
