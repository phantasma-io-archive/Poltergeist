using Poltergeist;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public Texture WalletLogo { get; private set; }
    public Texture MasterLogo { get; private set; }
    public Texture Dropshadow { get; private set; }
    public Texture NftAudioPlaceholder { get; private set; }
    public Texture NftPhotoPlaceholder { get; private set; }
    public Texture NftVideoPlaceholder { get; private set; }

    void Start()
    {
        WalletLogo = GetToken("soul", PlatformKind.Phantasma);
        MasterLogo = Resources.Load<Texture>("soul_master");
        Dropshadow = Resources.Load<Texture>("dropshadow");
        NftAudioPlaceholder = Resources.Load<Texture>("nft_audio_placeholder");
        NftPhotoPlaceholder = Resources.Load<Texture>("nft_photo_placeholder");
        NftVideoPlaceholder = Resources.Load<Texture>("nft_video_placeholder");
    }

    private Dictionary<string, Texture> _symbols = new Dictionary<string, Texture>();
    
    public void UnloadTokens()
    {
        _symbols.Clear();
    }

    //https://github.com/CityOfZion/neon-wallet/tree/dev/app/assets/nep5/png
    public Texture GetToken(string symbol, PlatformKind platform)
    {
        if (_symbols.ContainsKey(symbol))
        {
            return _symbols[symbol];
        }

        var texture = Resources.Load<Texture>($"Skins/{AccountManager.Instance.Settings.uiThemeName}/Tokens/" + symbol);

        if(texture == null)
            texture = Resources.Load<Texture>("Tokens/" + symbol);

        if(texture == null)
        {
            texture = Resources.Load<Texture>("Tokens/UNKNOWN_TOKEN_" + platform.ToString().ToUpper());
        }

        _symbols[symbol] = texture;

        return texture;
    }

    public static Texture2D TextureFromColor(Color color)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        texture.SetPixels(new Color[] { color, color, color, color });
        texture.Apply();
        return texture;
    }
}
