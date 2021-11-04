using UnityEngine;
using UnityEngine.Networking;
using Phantasma.SDK;
using LunarLabs.Parser;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Parsing and storing data received from GAME store.
public static class GameStore
{
    public static void Clear()
    {
        StoreNft.Clear();
    }

    public struct Nft
    {
        public string ID;
        public string chainName;
        public string creatorAddress;
        public UInt64 mint;
        public string ownerAddress;

        // parsed rom
        public UInt64 app_index;
        public string extra;
        public string img_url;
        public string info_url;
        public UInt64 item_id;
        public string metadata;
        public string mintedFor;
        public string seed;
        public UInt64 timestamp;
        public DateTime timestampDT;
        public UInt64 type;

        public string ram;
        public string rom;
        public string series;
        public string status;
        public string pavillion_id;

        // meta
        public UInt64 available_from;
        public string current_hash;
        public string description_english;
        public string itemdefid;
        public UInt64 modified_timestamp;
        public string name_english;
        public UInt64 price_usd_cent;
        public string meta_type;
    }

    private static Hashtable StoreNft = new Hashtable();


    public static bool CheckIfNftLoaded(string id)
    {
        return StoreNft.Contains(id);
    }

    public static Nft GetNft(string id)
    {
        return StoreNft.Contains(id) ? (Nft)StoreNft[id] : new Nft();
    }

    private static void LoadStoreNftFromDataNode(DataNode storeNft, Action<Nft> callback)
    {
        if (storeNft == null)
        {
            return;
        }

        var nfts = storeNft.GetNode("nfts");
        var meta = storeNft.GetNode("meta");

        foreach (DataNode item in nfts.Children)
        {
            var currentId = item.GetString("ID");

            var nft = GetNft(currentId);
            var newNft = String.IsNullOrEmpty(nft.ID); // There's no such NFT in StoreNft yet.

            nft.ID = currentId;
            nft.chainName = item.GetString("chainName");
            nft.creatorAddress = item.GetString("creatorAddress");
            nft.mint = item.GetUInt32("mint");
            nft.ownerAddress = item.GetString("ownerAddress");
            
            var parsedRom = item.GetNode("parsed_rom");
            
            nft.app_index = parsedRom.GetUInt32("app_index");
            nft.extra = parsedRom.GetString("extra");
            nft.img_url = parsedRom.GetString("img_url");
            nft.info_url = parsedRom.GetString("info_url");
            nft.item_id = parsedRom.GetUInt32("item_id");
            nft.metadata = parsedRom.GetString("metadata");
            nft.mintedFor = parsedRom.GetString("mintedFor");
            nft.seed = parsedRom.GetString("seed");
            nft.timestamp = parsedRom.GetUInt32("timestamp");
            nft.timestampDT = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(nft.timestamp).ToLocalTime();
            nft.type = parsedRom.GetUInt32("type");

            nft.ram = item.GetString("ram");
            nft.rom = item.GetString("rom");
            nft.series = item.GetString("series");
            nft.status = item.GetString("status");
            nft.pavillion_id = item.GetString("pavillion_id");

            var metaNode = meta.GetNode(nft.metadata);
            nft.available_from = metaNode.GetUInt32("available_from");
            nft.current_hash = metaNode.GetString("current_hash");
            nft.description_english = metaNode.GetString("description_english");
            nft.itemdefid = metaNode.GetString("itemdefid");
            nft.modified_timestamp = metaNode.GetUInt32("modified_timestamp");
            nft.name_english = metaNode.GetString("name_english");
            nft.price_usd_cent = metaNode.GetUInt32("price_usd_cent");
            nft.meta_type = metaNode.GetString("type");

            if (newNft)
                StoreNft.Add(currentId, nft);

            callback(nft);
        }
    }

    public static IEnumerator LoadStoreNft(string[] ids, Action<Nft> onItemLoadedCallback, Action onAllItemsLoadedCallback)
    {
        var url = "https://pavillionhub.com/api/nft_data?phantasma_ids=1&token=GAME&meta=1&ids=";
        var storeNft = Cache.GetDataNode("game-store-nft", Cache.FileType.JSON, 60 * 24);
        if (storeNft != null)
        {
            LoadStoreNftFromDataNode(storeNft, onItemLoadedCallback);

            // Checking, that cache contains all needed NFTs.
            string[] missingIds = ids;
            for (int i = 0; i < ids.Length; i++)
            {
                if (CheckIfNftLoaded(ids[i]))
                {
                    missingIds = missingIds.Where(x => x != ids[i]).ToArray();
                }
            }
            ids = missingIds;

            if (ids.Length == 0)
            {
                onAllItemsLoadedCallback();
                yield break;
            }
        }

        var idList = "";
        for (int i = 0; i < ids.Length; i++)
        {
            if (String.IsNullOrEmpty(idList))
                idList += ids[i];
            else
                idList += "," + ids[i];
        }

        yield return WebClient.RESTRequest(url + idList, 0, (error, msg) =>
        {
            Log.Write("LoadStoreNft() error: " + error);
        },
        (response) =>
        {
            if (response != null)
            {
                LoadStoreNftFromDataNode(response, onItemLoadedCallback);

                if (storeNft != null)
                {
                    // Cache already exists, need to add new nfts to existing cache.
                    foreach (var node in response.Children)
                    {
                        storeNft.AddNode(node);
                    }
                }
                else
                {
                    storeNft = response;
                }
                if (storeNft != null)
                    Cache.Add("game-store-nft", Cache.FileType.JSON, DataFormats.SaveToString(DataFormat.JSON, storeNft));
            }
            onAllItemsLoadedCallback();
        });
    }

    private static string NftToString(Nft nft)
    {
        return "Item #: " + nft.ID + "\n" +
            "chainName: " + nft.chainName + "\n" +
            "creatorAddress: " + nft.creatorAddress + "\n" +
            "mint: " + nft.mint + "\n" +
            "ownerAddress: " + nft.ownerAddress + "\n" +
            "app_index: " + nft.app_index + "\n" +
            "extra: " + nft.extra + "\n" +
            "img_url: " + nft.img_url + "\n" +
            "info_url: " + nft.info_url + "\n" +
            "item_id: " + nft.item_id + "\n" +
            "metadata: " + nft.metadata + "\n" +
            "mintedFor: " + nft.mintedFor + "\n" +
            "seed: " + nft.seed + "\n" +
            "timestamp: " + nft.timestamp + "\n" +
            "type: " + nft.type + "\n" +
            "ram: " + nft.ram + "\n" +
            "rom: " + nft.rom + "\n" +
            "series: " + nft.series + "\n" +
            "status: " + nft.status + "\n" +
            "pavillion_id: " + nft.pavillion_id + "\n" +
            "available_from: " + nft.available_from + "\n" +
            "current_hash: " + nft.current_hash + "\n" +
            "description_english: " + nft.description_english + "\n" +
            "itemdefid: " + nft.itemdefid + "\n" +
            "modified_timestamp: " + nft.modified_timestamp + "\n" +
            "name_english: " + nft.name_english + "\n" +
            "price_usd_cent: " + nft.price_usd_cent + "\n" +
            "type: " + nft.meta_type;
    }

    public static void LogStoreNft()
    {
        for (int i = 0; i < StoreNft.Count; i++)
        {
            Log.Write(NftToString((Nft)StoreNft[i]));
        }
    }
}
