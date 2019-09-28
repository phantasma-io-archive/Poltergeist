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

    public Texture WalletLogo;

    void Start()
    {
        WalletLogo = GetToken("soul");
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
