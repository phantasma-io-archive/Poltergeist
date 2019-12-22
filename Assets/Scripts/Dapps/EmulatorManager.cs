using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LunarLabs.Retro;
using Poltergeist;

public class EmulatorManager : MonoBehaviour
{
    public static EmulatorManager Instance { get; private set; }

    public Texture2D screenTexture { get; private set; }

    private RetroConsole console;

    public bool HasROM { get; private set; }

    private float lastTick;

    private UnityEngine.Color32[] pixels;

    private void Awake()
    {
        Instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        console = new RetroConsole(() => Time.time);
        screenTexture = new Texture2D(console.ResolutionX, console.ResolutionY, TextureFormat.RGBA32, false, false);
        pixels = new UnityEngine.Color32[console.ResolutionX * console.ResolutionY];
    }

    // Update is called once per frame
    void Update()
    {
        if (!HasROM)
        {
            return;
        }

        var leftArrow = Input.GetKey(KeyCode.LeftArrow);
        var rightArrow = Input.GetKey(KeyCode.RightArrow);
        var upArrow = Input.GetKey(KeyCode.UpArrow);
        var downArrow = Input.GetKey(KeyCode.DownArrow);
        var buttonA = Input.GetKey(KeyCode.X);

        if (Input.GetMouseButton(0))
        {
            bool onMiddle = Input.mousePosition.y > Screen.height * 0.25f && Input.mousePosition.y < Screen.height * 0.75f;

            if (onMiddle && Input.mousePosition.x <= Screen.width * 0.33f)
            {
                leftArrow = true;
            }
            else
            if (onMiddle && Input.mousePosition.x >= Screen.width * 0.66f)
            {
                rightArrow = true;
            }
            else
            if (Input.mousePosition.y <= Screen.height * 0.25f)
            {
                downArrow = true;
            }
            else
            if (Input.mousePosition.y >= Screen.height * 0.75f)
            {
                upArrow = true;
            }
            else
            {
                buttonA = true;
            }
        }

        var temp = ConsoleInputFlags.None;
        if (leftArrow) temp |= ConsoleInputFlags.Left;
        if (rightArrow) temp |= ConsoleInputFlags.Right;
        if (upArrow) temp |= ConsoleInputFlags.Up;
        if (downArrow) temp |= ConsoleInputFlags.Down;
        if (buttonA) temp |= ConsoleInputFlags.A;
        if (Input.GetKey(KeyCode.Z)) temp |= ConsoleInputFlags.B;
        if (Input.GetKey(KeyCode.C)) temp |= ConsoleInputFlags.Start;
        if (Input.GetKey(KeyCode.V)) temp |= ConsoleInputFlags.Select;

        var delta = Time.time - lastTick;
        if (delta >= 0.05f)
        {
            console.Update(temp);

            for (int j = 0; j < console.ResolutionY; j++)
            {
                for (int i = 0; i < console.ResolutionX; i++)
                {
                    int src = (i + j * console.ResolutionX) * 4;
                    int dest = i + ((console.ResolutionY - 1) - j) * console.ResolutionX;

                    var r = console.RGBAOutput[src]; src++;
                    var g = console.RGBAOutput[src]; src++;
                    var b = console.RGBAOutput[src]; src++;

                    pixels[dest] = new UnityEngine.Color32(r, g, b, 255);
                }
            }

            screenTexture.SetPixels32(pixels);
            screenTexture.Apply();

            lastTick = Time.time;
        }
    }

    public void LoadROM()
    {
        var data = Resources.Load<TextAsset>("tetris");
        console.LoadROM(data.bytes, new Tetrochain.TetrisProgram(WalletGUI.Instance));
        lastTick = Time.time;
        HasROM = true;
    }

    public void UnloadROM()
    {
        HasROM = false;
    }
}
