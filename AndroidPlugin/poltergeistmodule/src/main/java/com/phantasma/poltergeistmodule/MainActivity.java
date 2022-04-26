package com.phantasma.poltergeistmodule;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.util.Log;

import androidx.activity.result.ActivityResultCallback;
import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;

import com.unity3d.player.MultiWindowSupport;
import com.unity3d.player.UnityPlayer;

public class MainActivity extends UnityPlayerActivity {

    public static String sessionIdIntent = "sessionId";
    public static String openWalletIntent = "open_wallet";
    public static String sendTxIntent = "walletInteraction";
    private static String openWalletInput;
    private static String sendTxData;
    private static Intent lastIntent;

    public static String getMessage()
    {
        return "Plugin Reachable";
    }

    @Override
    protected void onCreate(Bundle var1)
    {
        Log.e("Unity-PG", "OnCreated");
        //HandleIntents();
        super.onCreate(var1);
    }

    @Override protected void onResume()
    {
        Log.e("Unity-PG", "OnResume");
        super.onResume();
        HandleIntents();
    }

    public void ReturnMessage(String msg){
        Log.e("Unity-PG", "OnReturnMessage");
        Intent result = lastIntent;
        result.putExtra("result", msg);
        setResult(Activity.RESULT_OK, result);
    }

    private void HandleIntents(){
        Intent intent = getIntent();
        lastIntent = intent;
        if(intent != null ) {
            if ( intent.hasExtra(openWalletIntent)){
                openWalletInput = getIntent().getStringExtra(openWalletIntent);
                Log.e("Unity-PG", openWalletInput);
                // call method inside unity
                UnityPlayer.UnitySendMessage("MainActivity", "CallMethodByName", openWalletInput);
            }

            if ( intent.hasExtra(sendTxIntent)){
                sendTxData = getIntent().getStringExtra(sendTxIntent);
                Log.e("Unity-PG", sendTxData);
                UnityPlayer.UnitySendMessage("MainActivity", "OnIntentCall", sendTxData);

            }
        }
    }
}