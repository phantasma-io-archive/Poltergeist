using LunarLabs.Parser;
using UnityEngine;
using Phantasma.SDK;
using Poltergeist;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Domain;
using System;

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
    public static void LoadFromText(string tokensJson, PlatformKind platform)
    {
        if (string.IsNullOrEmpty(tokensJson))
        {
            Log.WriteWarning($"Tokens.LoadFromText(): Cannot load {platform} symbols, tokensJson is empty.");
            return;
        }

        var externalTokens = LunarLabs.Parser.JSON.JSONReader.ReadFromString(tokensJson);

        if (externalTokens == null || externalTokens.Children == null)
        {
            Log.WriteWarning($"Tokens.LoadFromText(): Cannot load {platform} symbols - file is corrupted.");
            return;
        }

        foreach (var externalToken in externalTokens.Children)
        {
            var symbol = externalToken.GetString("symbol");
            var name = externalToken.GetString("name");
            var decimals = externalToken.GetInt32("decimals");
            var hash = externalToken.GetString("hash");
            var coinGeckoId = externalToken.GetString("coinGeckoId");

            var token = Tokens.GetToken(symbol, platform);
            if (token != null)
            {
                if (token.name == name && token.decimals == decimals)
                {
                    var tokenList = token.external.ToList();
                    if (tokenList.Any(x => x.platform == platform.ToString().ToLower()))
                    {
                        Log.WriteWarning($"Tokens.LoadFromText(): {platform} symbols: Token '{symbol}' already exists for platform.");
                    }
                    else
                    {
                        tokenList.Add(new TokenPlatform { platform = platform.ToString().ToLower(), hash = hash });
                        token.external = tokenList.ToArray();
                    }
                }
                else
                {
                    Log.WriteWarning($"Tokens.LoadFromText(): {platform} symbols: Token '{symbol}' already exists and is not compatible.");
                }
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

                if(!string.IsNullOrEmpty(coinGeckoId))
                {
                    token.apiSymbol = coinGeckoId;
                }

                SupportedTokens.Add(token);
            }
        }
    }
    public static void Load(PlatformKind platform)
    {
        // Currently will only work after restart.
        string testnet = "";
        if (platform == PlatformKind.BSC && AccountManager.Instance.Settings.binanceSmartChainNetwork == BinanceSmartChainNetwork.Test_Net)
            testnet = ".Testnet";
        
        var resource = Resources.Load<TextAsset>($"Tokens.{platform.ToString().ToUpper()}{testnet}");

        if (resource == null || string.IsNullOrEmpty(resource.text))
        {
            Log.WriteWarning($"Tokens.Load(): Cannot load {platform} symbols.");
            return;
        }

        Tokens.LoadFromText(resource.text, platform);
    }
    public static void LoadCoinGeckoSymbols()
    {
        // First we init all fungible token API IDs with default values.
        SupportedTokens.ForEach(x => { if (string.IsNullOrEmpty(x.apiSymbol) && x.IsFungible()) { x.apiSymbol = x.symbol.ToLower(); } });

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
            var tokens = Tokens.GetTokens(symbol);
            if (tokens.Length > 0)
            {
                for (var i = 0; i < tokens.Length; i++)
                {
                    if (apiSymbol == "-") // Means token has no CoinGecko API ID.
                        tokens[i].apiSymbol = "";
                    else
                        tokens[i].apiSymbol = apiSymbol;
                }
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
            try
            {
                neoToken.external.Where(x => x.platform.ToUpper() == "NEO").Single().hash = "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b";
            }
            catch(Exception e)
            {
                Log.Write($"NEO token hash registration exception: {e}");
            }
            var gasToken = Tokens.GetToken("GAS", PlatformKind.Neo);
            try
            {
                gasToken.external.Where(x => x.platform.ToUpper() == "NEO").Single().hash = "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";
            }
            catch (Exception e)
            {
                Log.Write($"GAS token hash registration exception: {e}");
            }

            Tokens.Load(PlatformKind.Ethereum);
            Tokens.Load(PlatformKind.Neo);
            Tokens.Load(PlatformKind.BSC);

            var accountManager = AccountManager.Instance;
            Tokens.LoadFromText(accountManager.Settings.ethereumUserTokens, PlatformKind.Ethereum);
            Tokens.LoadFromText(accountManager.Settings.neoUserTokens, PlatformKind.Neo);
            Tokens.LoadFromText(accountManager.Settings.binanceSmartChainUserTokens, PlatformKind.BSC);

            Tokens.LoadCoinGeckoSymbols();

            Log.Write($"{Tokens.GetTokens().Length} tokens supported");

            Tokens.ToLog();
        }
    }

    public static Token[] GetTokens(string symbol)
    {
        return SupportedTokens.Where(x => x.symbol.ToUpper() == symbol.ToUpper())
            .ToArray();
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

            var hash = token.external.Where(x => x.platform.ToUpper() == platform.ToString().ToUpper()).SingleOrDefault()?.hash;

            if (hash != null && hash.StartsWith("0x"))
                hash = hash.Substring(2);

            return hash;
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
        foreach (var token in SupportedTokens)
        {
            tokens += token.ToString() + "\n";
        }
        Log.Write("Supported tokens:\n" + tokens);
    }

    private static DataNode UserTokensUnserialize(string tokensJson)
    {
        if (string.IsNullOrEmpty(tokensJson))
        {
            return null;
        }

        return LunarLabs.Parser.JSON.JSONReader.ReadFromString(tokensJson);
    }

    private static string UserTokensSerialize(DataNode tokensJson)
    {
        if (tokensJson == null)
        {
            return null;
        }

        return DataFormats.SaveToString(DataFormat.JSON, tokensJson);
    }
    private static void UserTokenAdd(ref string tokensJson, string tokenSymbol, string tokenName, int tokenDecimals, string tokenHash, string coinGeckoId)
    {
        var userTokens = Tokens.UserTokensUnserialize(tokensJson);

        if (userTokens == null)
        {
            userTokens = DataNode.CreateArray();
        }

        var tokenNode = userTokens.AddNode(DataNode.CreateObject());

        tokenNode.AddField("symbol", tokenSymbol.ToUpper());
        tokenNode.AddField("name", tokenName);
        tokenNode.AddField("decimals", tokenDecimals);
        tokenNode.AddField("hash", tokenHash);
        tokenNode.AddField("coinGeckoId", coinGeckoId);

        tokensJson = UserTokensSerialize(userTokens);
    }
    private static bool UserTokenEdit(ref string tokensJson, string tokenSymbol, string tokenName, int tokenDecimals, string tokenHash, string coinGeckoId)
    {
        var userTokens = Tokens.UserTokensUnserialize(tokensJson);

        if (userTokens == null)
        {
            return false;
        }

        DataNode tokenNode = null;
        foreach (var node in userTokens.Children)
        {
            if (node.GetString("symbol").ToUpper() == tokenSymbol.ToUpper())
            {
                tokenNode = node;
            }
        }

        if (tokenNode == null)
        {
            // We couldn't find token node.
            return false;
        }

        tokenNode.GetNode("symbol").Value = tokenSymbol;
        
        var nameNode = tokenNode.GetNode("name");
        if (nameNode == null)
        {
            tokenNode.AddField("name", tokenName);
        }
        else
        {
            nameNode.Value = tokenName;
        }

        var decimalsNode = tokenNode.GetNode("decimals");
        if (decimalsNode == null)
        {
            tokenNode.AddField("decimals", tokenDecimals);
        }
        else
        {
            decimalsNode.Value = tokenDecimals.ToString();
        }

        var hashNode = tokenNode.GetNode("hash");
        if (hashNode == null)
        {
            tokenNode.AddField("hash", tokenHash);
        }
        else
        {
            hashNode.Value = tokenHash;
        }

        var coinGeckoIdNode = tokenNode.GetNode("coinGeckoId");
        if (coinGeckoIdNode == null)
        {
            tokenNode.AddField("coinGeckoId", coinGeckoId);
        }
        else
        {
            coinGeckoIdNode.Value = coinGeckoId;
        }

        tokensJson = UserTokensSerialize(userTokens);

        return true;
    }
    private static bool UserTokenDelete(ref string tokensJson, string tokenSymbol)
    {
        var userTokens = Tokens.UserTokensUnserialize(tokensJson);

        if (userTokens == null)
        {
            return false;
        }

        var newUserTokens = DataNode.CreateArray();
        var tokenFound = false;

        foreach (var node in userTokens.Children)
        {
            if (node.GetString("symbol").ToUpper() == tokenSymbol.ToUpper())
            {
                tokenFound = true;
            }
            else
            {
                // Copy to new list all tokens except the one that is being deleted.
                var tokenNode = newUserTokens.AddNode(DataNode.CreateObject());

                tokenNode.AddField("symbol", node.GetString("symbol"));
                tokenNode.AddField("name", node.GetString("name"));
                tokenNode.AddField("decimals", node.GetInt32("decimals"));
                tokenNode.AddField("hash", node.GetString("hash"));
            }
        }

        tokensJson = UserTokensSerialize(newUserTokens);

        return tokenFound;
    }
    public static void UserTokenAdd(PlatformKind platform, string tokenSymbol, string tokenName, int tokenDecimals, string tokenHash, string coinGeckoId)
    {
        var accountManager = AccountManager.Instance;
        switch (platform)
        {
            case PlatformKind.Ethereum:
                UserTokenAdd(ref accountManager.Settings.ethereumUserTokens, tokenSymbol, tokenName, tokenDecimals, tokenHash, coinGeckoId);
                break;
            case PlatformKind.Neo:
                UserTokenAdd(ref accountManager.Settings.neoUserTokens, tokenSymbol, tokenName, tokenDecimals, tokenHash, coinGeckoId);
                break;
            default:
                Log.WriteError($"Addition of user tokens for platform {platform} is not supported");
                break;
        }
    }
    public static bool UserTokenEdit(PlatformKind platform, string tokenSymbol, string tokenName, int tokenDecimals, string tokenHash, string coinGeckoId)
    {
        bool result = false;

        var accountManager = AccountManager.Instance;
        switch (platform)
        {
            case PlatformKind.Ethereum:
                result = UserTokenEdit(ref accountManager.Settings.ethereumUserTokens, tokenSymbol, tokenName, tokenDecimals, tokenHash, coinGeckoId);
                break;
            case PlatformKind.Neo:
                result = UserTokenEdit(ref accountManager.Settings.neoUserTokens, tokenSymbol, tokenName, tokenDecimals, tokenHash, coinGeckoId);
                break;
            default:
                Log.WriteError($"Editing of user tokens for platform {platform} is not supported");
                break;
        }

        return result;
    }
    public static bool UserTokenDelete(PlatformKind platform, string tokenSymbol)
    {
        bool result = false;

        var accountManager = AccountManager.Instance;
        switch (platform)
        {
            case PlatformKind.Ethereum:
                result = UserTokenDelete(ref accountManager.Settings.ethereumUserTokens, tokenSymbol);
                break;
            case PlatformKind.Neo:
                result = UserTokenDelete(ref accountManager.Settings.neoUserTokens, tokenSymbol);
                break;
            default:
                Log.WriteError($"Deletion of user tokens for platform {platform} is not supported");
                break;
        }

        return result;
    }
    public static void UserTokensDeleteAll()
    {
        var accountManager = AccountManager.Instance;
        accountManager.Settings.ethereumUserTokens = null;
        accountManager.Settings.neoUserTokens = null;
    }
    public static string UserTokensGet(PlatformKind platform)
    {
        var accountManager = AccountManager.Instance;
        switch (platform)
        {
            case PlatformKind.Ethereum:
                return accountManager.Settings.ethereumUserTokens;
            case PlatformKind.Neo:
                return accountManager.Settings.neoUserTokens;
            default:
                Log.WriteError($"Retrival of user tokens for platform {platform} is not supported");
                break;
        }

        return null;
    }
    public static bool UserTokensSet(PlatformKind platform, string tokensJson)
    {
        if (!string.IsNullOrEmpty(tokensJson))
        {
            try
            {
                var testResult = LunarLabs.Parser.JSON.JSONReader.ReadFromString(tokensJson);

                if(testResult == null)
                {
                    return false;
                }
            }
            catch(Exception)
            {
                return false;
            }
        }

        var accountManager = AccountManager.Instance;
        switch (platform)
        {
            case PlatformKind.Ethereum:
                accountManager.Settings.ethereumUserTokens = tokensJson;
                return true;
            case PlatformKind.Neo:
                accountManager.Settings.neoUserTokens = tokensJson;
                return true;
            default:
                Log.WriteError($"Setting of user tokens for platform {platform} is not supported");
                return false;
        }
    }
}
