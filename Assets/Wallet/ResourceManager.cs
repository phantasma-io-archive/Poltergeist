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

    void Start()
    {
        WalletLogo = GetToken("soul");
        MasterLogo = Resources.Load<Texture>("soul_master");
    }

    private Dictionary<string, Texture> _symbols = new Dictionary<string, Texture>();

    //https://github.com/CityOfZion/neon-wallet/tree/dev/app/assets/nep5/png
    public Texture GetToken(string symbol)
    {
        if (_symbols.ContainsKey(symbol))
        {
            return _symbols[symbol];
        }

        var texture = Resources.Load<Texture>("Tokens/" + symbol);
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
