using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Policy;
using System.Text.RegularExpressions;
using Poltergeist;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class UpdateChecker : MonoBehaviour
{
    public string githubOwner = "phantasma-io"; // Your GitHub username
    public string githubRepo = "Poltergeist"; // Your repository name
    public string currentVersion = "2.8.3"; // Current version of your game

    private const string GITHUB_RELEASES_URL = "https://github.com/";
    private static string URL = "";
    
    public static string UPDATE_URL => URL;


    // Start is called before the first frame update
    private void Start()
    {
        URL = GITHUB_RELEASES_URL + githubOwner + "/" + githubRepo + "/releases/latest";
        currentVersion = Application.version;
        StartCoroutine(CheckForUpdates());
    }


    private IEnumerator CheckForUpdates()
    {
        using (UnityWebRequest www =
               UnityWebRequest.Get(GITHUB_RELEASES_URL + githubOwner + "/" + githubRepo + "/releases/latest"))
        {
            www.SetRequestHeader("User-Agent", "Unity");
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
            }
            else
            {
                string htmlContent = www.downloadHandler.text;
                string versionPattern = @"<h1 data-view-component=""true"" class=""d-inline mr-3"">(.*?)</h1>";
                Match versionMatch = Regex.Match(htmlContent, versionPattern);

                if (versionMatch.Success)
                {
                    string latestVersion = versionMatch.Groups[1].Value;
                    string latestVersionNoPrefix =
                        latestVersion.StartsWith("v") ? latestVersion.Substring(1) : latestVersion;
                    string currentVersionNoPrefix = Application.version.StartsWith("v")
                        ? Application.version.Substring(1)
                        : Application.version;

                    System.Version latestVer = new System.Version(latestVersionNoPrefix);
                    System.Version currentVer = new System.Version(currentVersionNoPrefix);

                    if (latestVer > currentVer)
                    {
                        WalletGUI.Instance.ShowUpdateModal("Update Available",
                            "A new version of the wallet is available. Please update the wallet.\n\n\n"+
                            $"{URL}", () =>
                            {
                                Debug.Log("Close");
                            });
                    }
                }
                else
                {
                    Debug.LogError("Could not find version information.");
                }
            }
        }
    }
}
