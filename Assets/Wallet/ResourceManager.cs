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
    public Texture CloseLogo { get; private set; }

    void Start()
    {
        WalletLogo = GetToken("soul");
        CloseLogo = Resources.Load<Texture>("close");
    }

    private Dictionary<string, Texture> _symbols = new Dictionary<string, Texture>();

    public Texture GetToken(string symbol)
    {
        symbol = symbol.ToLower();
        if (_symbols.ContainsKey(symbol))
        {
            return _symbols[symbol];
        }

        var texture = Resources.Load<Texture>("Tokens/" + symbol);
        _symbols[symbol] = texture;

        return texture;
    }
}
