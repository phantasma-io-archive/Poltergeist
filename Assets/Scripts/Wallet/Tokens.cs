using LunarLabs.Parser;
using UnityEngine;
using Phantasma.SDK;
using Poltergeist;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Domain;

public static class Tokens
{
    public static List<Token> SupportedTokens = new List<Token>();

    public static void Reset()
    {
        SupportedTokens.Clear();
    }
    public static void AddTokens(Token[] tokens)
    {
        SupportedTokens.AddRange(tokens);
    }
    public static void Load(PlatformKind platform)
    {
        var resource = Resources.Load<TextAsset>($"Tokens.{platform.ToString().ToUpper()}");

        if (resource == null || string.IsNullOrEmpty(resource.text))
        {
            Log.WriteWarning($"Cannot load {platform} symbols.");
            return;
        }

        var externalTokens = LunarLabs.Parser.JSON.JSONReader.ReadFromString(resource.text);

        if (externalTokens == null || externalTokens.Children == null)
        {
            Log.WriteWarning($"Cannot load {platform} symbols - file is corrupted.");
            return;
        }

        foreach (var externalToken in externalTokens.Children)
        {
            var symbol = externalToken.GetString("symbol");
            var name = externalToken.GetString("name");
            var decimals = externalToken.GetInt32("decimals");
            var hash = externalToken.GetString("hash");

            var token = Tokens.GetToken(symbol);
            if (token != null)
            {
                Log.WriteWarning($"{platform} symbols: Token '{symbol}' already exists.");
            }
            else
            {
                token = new Token();
                token.mainnetToken = false;
                token.symbol = symbol;
                token.name = name;
                token.decimals = decimals;
                token.flags = TokenFlags.Transferable.ToString() + "," + TokenFlags.Fungible.ToString();
                if (decimals > 0)
                    token.flags += "," + TokenFlags.Divisible.ToString();
                token.external = new TokenPlatform[] { new TokenPlatform { platform = platform.ToString().ToLower(), hash = hash } };

                SupportedTokens.Add(token);
            }
        }
    }
    public static void LoadCoinGeckoSymbols()
    {
        // First we init all fungible token API IDs with default values.
        SupportedTokens.ForEach(x => { if (x.IsFungible()) { x.apiSymbol = x.symbol.ToLower(); } });

        // Then apply IDs from config.
        var resource = Resources.Load<TextAsset>("Tokens.CoinGecko");
        
        if (resource == null || string.IsNullOrEmpty(resource.text))
        {
            Log.WriteWarning("Cannot load CoinGecko symbols.");
            return;
        }

        var tokenApiSymbols = LunarLabs.Parser.JSON.JSONReader.ReadFromString(resource.text);

        if (tokenApiSymbols == null || tokenApiSymbols.Children == null)
        {
            Log.WriteWarning("Cannot load CoinGecko symbols - file is corrupted.");
            return;
        }

        foreach (var tokenApiSymbol in tokenApiSymbols.Children)
        {
            var symbol = tokenApiSymbol.GetString("symbol");
            var apiSymbol = tokenApiSymbol.GetString("apiSymbol");
            var token = Tokens.GetToken(symbol);
            if (token != null)
            {
                if(apiSymbol == "-") // Means token has no CoinGecko API ID.
                    token.apiSymbol = "";
                else
                    token.apiSymbol = apiSymbol;
            }
            else
            {
                Log.WriteWarning($"CoinGecko symbols: Token '{symbol}' not found.");
            }
        }
    }
    public static void Init(Token[] mainnetTokens)
    {
        lock (string.Intern("TokensInit"))
        {
            Tokens.Reset();

            Tokens.AddTokens(mainnetTokens);

            // TODO remove after mainnet fix.
            // Fix for incorrect hash returned by BP.
            var neoToken = Tokens.GetToken("NEO", PlatformKind.Neo);
            neoToken.external.Where(x => x.platform.ToUpper() == "NEO").Single().hash = "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b";
            var gasToken = Tokens.GetToken("GAS", PlatformKind.Neo);
            gasToken.external.Where(x => x.platform.ToUpper() == "NEO").Single().hash = "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";

            Tokens.Load(PlatformKind.Ethereum);
            Tokens.Load(PlatformKind.Neo);

            Tokens.LoadCoinGeckoSymbols();

            Log.Write($"{Tokens.GetTokens().Length} tokens supported");

            Tokens.ToLog();
        }
    }

    public static Token GetToken(string symbol)
    {
        return SupportedTokens.Where(x => x.symbol.ToUpper() == symbol.ToUpper())
            .SingleOrDefault();
    }
    public static Token GetToken(string symbol, PlatformKind platform)
    {
        return SupportedTokens.Where(x => x.symbol.ToUpper() == symbol.ToUpper() &&
            ((platform == PlatformKind.Phantasma && x.mainnetToken == true) ||
            (platform != PlatformKind.Phantasma && x.external != null && x.external.Any(y => y.platform.ToUpper() == platform.ToString().ToUpper()))))
            .SingleOrDefault();
    }
    public static bool HasToken(string symbol, PlatformKind platform)
    {
        return SupportedTokens.Any(x => x.symbol.ToUpper() == symbol.ToUpper() &&
            ((platform == PlatformKind.Phantasma && x.mainnetToken == true) ||
            (platform != PlatformKind.Phantasma && x.external != null && x.external.Any(y => y.platform.ToUpper() == platform.ToString().ToUpper()))));
    }
    public static bool GetToken(string symbol, PlatformKind platform, out Token token)
    {
        token = GetToken(symbol, platform);
        if (token != default(Token))
        {
            return true;
        }

        token = new Token();
        return false;
    }
    public static Token[] GetTokens()
    {
        return SupportedTokens.ToArray();
    }
    public static Token[] GetTokens(PlatformKind platform)
    {
        return SupportedTokens.Where(x => (platform == PlatformKind.Phantasma && x.mainnetToken == true) ||
            (platform != PlatformKind.Phantasma && x.external != null && x.external.Any(y => y.platform.ToUpper() == platform.ToString().ToUpper())))
            .ToArray();
    }
    public static Token[] GetTokensForCoingecko()
    {
        return SupportedTokens.Where(x => string.IsNullOrEmpty(x.apiSymbol) == false)
            .ToArray();
    }
    public static int GetTokenDecimals(string symbol, PlatformKind platform)
    {
        var token = GetToken(symbol, platform);
        if (token != default(Token))
        {
            return token.decimals;
        }

        return -1;
    }
    public static string GetTokenHash(string symbol, PlatformKind platform)
    {
        var token = GetToken(symbol, platform);
        if (token != default(Token))
        {
            if (token.external == null)
                return null;

            return token.external.Where(x => x.platform.ToUpper() == platform.ToString().ToUpper()).SingleOrDefault()?.hash;
        }

        return null;
    }
    public static string GetTokenHash(Token token, PlatformKind platform)
    {
        if (token != default(Token))
        {
            if (token.external == null)
                return null;

            return token.external.Where(x => x.platform.ToUpper() == platform.ToString().ToUpper()).SingleOrDefault()?.hash;
        }

        return null;
    }

    public static void ToLog()
    {
        var tokens = "";
        foreach(var token in SupportedTokens)
        {
            tokens += token.ToString() + "\n";
        }
        Log.Write("Supported tokens:\n" + tokens);
    }
}
