using LunarLabs.Retro;
using LunarLabs.Retro.Resources;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tetrochain
{
    public interface IWalletConnector
    {
        Address GetAddress();
        Dictionary<string, decimal> GetBalances(string chain);
        void ExecuteTransaction(string description, byte[] script, string chain, Action<Hash> callback);
        void InvokeScript(string chain, byte[] script, Action<byte[]> callback);
    }

    public enum MenuState
    {
        Intro,
        Boards,
        Game,
        Wait,
        Error
    }

    public struct BoardInfo
    {
        public string Name;
        public string Symbol;
        public BigInteger Cost;
        public byte[] Pieces;
        public uint Seed;
        public BigInteger Pot;

        public int Decimals => Symbol == DomainSettings.FuelTokenSymbol ? DomainSettings.FuelTokenDecimals : DomainSettings.StakingTokenDecimals;

        public BoardInfo(string name, string symbol, BigInteger cost, byte[] pieces, uint seed, BigInteger pot)
        {
            Name = name;
            Symbol = symbol;
            Cost = cost;
            Pieces = pieces;
            Seed = seed;
            Pot = pot;
        }
    }

    //https://en.wikipedia.org/wiki/Pentomino
    // 21 kinds
    public enum PieceKind
    {
        Single,
        Duo,
        TrioI,
        TrioL,
        TetraI,
        TetraO,
        TetraZ,
        TetraT,
        TetraL,
        PentaF,
        PentaI,
        PentaL,
        PentaN,
        PentaP,
        PentaT,
        PentaU,
        PentaV,
        PentaW,
        PentaX,
        PentaY,
        PentaZ,
    }

    public struct PieceCoord
    {
        public int x;
        public int y;

        public PieceCoord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public struct PieceMove
    {
        public PieceKind piece;
        public int x;
        public int y;
        public int rot;

        public PieceMove(PieceKind piece, int x, int y, int rot)
        {
            this.piece = piece;
            this.x = x;
            this.y = y;
            this.rot = rot;
        }
    }

    public static class PieceUtils
    {
        public static PieceCoord[] GetCoords(PieceKind piece, int rotation)
        {
            PieceCoord[] coords;

            switch (piece)
            {
                case PieceKind.Single:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                    };
                    break;

                case PieceKind.Duo:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(1, 0),
                    };
                    break;

                case PieceKind.TrioI:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, -1),
                        new PieceCoord(0, 0),
                        new PieceCoord(0, 1),
                    };
                    break;

                case PieceKind.TrioL:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, -1),
                        new PieceCoord(0, 0),
                        new PieceCoord(1, 0),
                    };
                    break;


                case PieceKind.TetraI:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(1, 0),
                        new PieceCoord(2, 0),
                        new PieceCoord(3, 0),
                    };
                    break;

                case PieceKind.TetraO:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(1, 0),
                        new PieceCoord(1, 1),
                        new PieceCoord(0, 1),
                    };
                    break;

                case PieceKind.TetraZ:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(1, 0),
                        new PieceCoord(1, 1),
                        new PieceCoord(2, 1),
                    };
                    break;

                case PieceKind.TetraT:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(-1, 0),
                        new PieceCoord(0, 0),
                        new PieceCoord(1, 0),
                        new PieceCoord(0, 1),
                    };
                    break;


                case PieceKind.TetraL:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(1, 0),
                        new PieceCoord(2, 0),
                        new PieceCoord(0, 1),
                    };
                    break;

                case PieceKind.PentaF:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(-1, 0),
                        new PieceCoord(0, 1),
                        new PieceCoord(0, -1),
                        new PieceCoord(1, -1),
                    };
                    break;

                case PieceKind.PentaI:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(-2, 0),
                        new PieceCoord(-1, 0),
                        new PieceCoord(0, 0),
                        new PieceCoord(1, 0),
                        new PieceCoord(2, 0),
                    };
                    break;

                case PieceKind.PentaL:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(1, 0),
                        new PieceCoord(2, 0),
                        new PieceCoord(3, 0),
                        new PieceCoord(0, 1),
                    };
                    break;


                case PieceKind.PentaN:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(-1, 0),
                        new PieceCoord(0, 1),
                        new PieceCoord(1, 1),
                        new PieceCoord(2, 1),
                    };
                    break;

                case PieceKind.PentaP:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(1, 0),
                        new PieceCoord(2, 0),
                        new PieceCoord(2, 1),
                        new PieceCoord(1, 1),
                    };
                    break;

                case PieceKind.PentaT:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(0, 1),
                        new PieceCoord(-1, -1),
                        new PieceCoord(0, -1),
                        new PieceCoord(1, -1),
                    };
                    break;

                case PieceKind.PentaU:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(-1, 0),
                        new PieceCoord(-1, 1),
                        new PieceCoord(1, 0),
                        new PieceCoord(1, 1),
                    };
                    break;

                case PieceKind.PentaV:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(0, 1),
                        new PieceCoord(0, 2),
                        new PieceCoord(1, 0),
                        new PieceCoord(2, 0),
                    };
                    break;

                case PieceKind.PentaW:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(-1, 0),
                        new PieceCoord(-1, -1),
                        new PieceCoord(0, 1),
                        new PieceCoord(1, 1),
                    };
                    break;

                case PieceKind.PentaX:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(-1, 0),
                        new PieceCoord(1, 0),
                        new PieceCoord(0, -1),
                        new PieceCoord(0, 1),
                    };
                    break;

                case PieceKind.PentaY:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(0, -1),
                        new PieceCoord(1, 0),
                        new PieceCoord(-1, 0),
                        new PieceCoord(-2, 0),
                    };
                    break;

                case PieceKind.PentaZ:
                    coords = new PieceCoord[]
                    {
                        new PieceCoord(0, 0),
                        new PieceCoord(0, -1),
                        new PieceCoord(-1, -1),
                        new PieceCoord(0, 1),
                        new PieceCoord(1, 1),
                    };
                    break;

                default:
                    throw new Exception("Invalid piece type");
            }

            switch (rotation)
            {
                case 1:
                    for (int i = 0; i < coords.Length; i++)
                    {
                        var temp = coords[i];
                        coords[i] = new PieceCoord() { x = -temp.y, y = temp.x };
                    }
                    break;

                case 2:
                    for (int i = 0; i < coords.Length; i++)
                    {
                        var temp = coords[i];
                        coords[i] = new PieceCoord() { x = -temp.x, y = -temp.y };
                    }
                    break;

                case 3:
                    for (int i = 0; i < coords.Length; i++)
                    {
                        var temp = coords[i];
                        coords[i] = new PieceCoord() { x = temp.y, y = -temp.x };
                    }
                    break;
            }

            return coords;
        }
    }

    public class TetrisProgram : RetroProgram
    {
        private MenuState menuState;

        private string errorMsg;

        private PieceKind currentPiece;
        private int currentX;
        private int currentY;
        private int currentRotation;
        private int currentIndex;
        private uint currentTime;
        private uint spawnTime;

        private int dropY;
        private bool dropping;

        private bool gameover;

        private bool merging;
        private uint mergeTime;
        private int mergeY;
        private int mergeCombo;

        private int score;
        private int lines;

        private const int MaxSpeed = 20;
        private const int PieceSize = 10;
        private const int BoardWidth = 10;
        private const int BoardHeight = 13;

        private byte[] board;

        private List<PieceMove> moves = new List<PieceMove>();

        private static Random rnd = new Random();

        private int blockTileset;
        private int fontTileset;
        private int iconTileset;

        private List<BoardInfo> boardList = new List<BoardInfo>();

        private BoardInfo currentBoard;
        private List<PieceKind> pieceQueue = new List<PieceKind>();

        private int boardIndex;
        private IWalletConnector wallet;

        public TetrisProgram(IWalletConnector wallet)
        {
            this.wallet = wallet;
        }

        public override void Reset()
        {
            blockTileset = Console.ROM.FindResource("blocks", ResourceKind.Tileset);
            iconTileset = Console.ROM.FindResource("icons", ResourceKind.Tileset);
            fontTileset = Console.ROM.FindResource("retro", ResourceKind.Font);

            menuState = MenuState.Boards;

            board = new byte[BoardWidth * BoardHeight];

            boardList.Add(new BoardInfo("easy", "KCAL", UnitConversion.ToBigInteger(1, DomainSettings.FuelTokenDecimals),
                 new byte[] { 5, 5, 5, 5, 5, 5, 5, 5, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 123, UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals)));

            boardList.Add(new BoardInfo("hard", "SOUL", UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals),
             new byte[] { 0, 0, 0, 0, 0, 0, 0, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 }, 123, UnitConversion.ToBigInteger(200, DomainSettings.StakingTokenDecimals)));


            boardIndex = 0;
            pieceQueue.Clear();
        }

        private void PushError(string msg)
        {
            menuState = MenuState.Error;
            errorMsg = msg;
        }

        public static List<T> Shuffle<T>(List<T> list)
        {
            var source = list.ToList();
            int n = source.Count;
            var shuffled = new List<T>(n);
            shuffled.AddRange(source);
            while (n > 1)
            {
                n--;
                int k = rnd.Next(n + 1);
                T value = shuffled[k];
                shuffled[k] = shuffled[n];
                shuffled[n] = value;
            }
            return shuffled;
        }

        private void GeneratePiece()
        {
            dropping = false;
            dropY = 0;

            if (pieceQueue.Count == 0)
            {
                for (int i = 0; i < 21; i++)
                {
                    var piece = (PieceKind)i;
                    var amount = currentBoard.Pieces[i];

                    for (int n = 0; n < amount; n++)
                    {
                        pieceQueue.Add(piece);
                    }
                }

                pieceQueue = Shuffle<PieceKind>(pieceQueue);
            }

            currentPiece = pieceQueue[0];
            pieceQueue.RemoveAt(0);

            currentX = BoardWidth / 2;
            currentY = 0;
            currentRotation = 0;
            currentTime = Console.FrameCounter;
            currentIndex++;

            spawnTime = currentTime;

            gameover = HasCollision(0, 0);
        }

        private bool ValidCoord(int x, int y)
        {
            if (x < 0 || y < 0 || x >= BoardWidth || y >= BoardHeight)
            {
                return false;
            }

            return true;
        }

        private bool HasCollision(int dirX, int dirY)
        {
            var coords = PieceUtils.GetCoords(currentPiece, currentRotation);

            for (int i = 0; i < coords.Length; i++)
            {
                var coord = coords[i];
                coord.x += currentX + dirX;
                coord.y += currentY + dirY;

                if (coord.x < 0)
                {
                    return true;
                }

                if (coord.x >= BoardWidth)
                {
                    return true;
                }

                if (coord.y >= BoardHeight)
                {
                    return true;
                }

                if (ValidCoord(coord.x, coord.y))
                {
                    var offset = coord.x + coord.y * BoardWidth;
                    if (board[offset] > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void MergePiece()
        {
            var coords = PieceUtils.GetCoords(currentPiece, currentRotation);

            var color = (byte)(1 + currentIndex % 7);

            for (int i = 0; i < coords.Length; i++)
            {
                var coord = coords[i];
                coord.x += currentX;
                coord.y += currentY;

                if (ValidCoord(coord.x, coord.y))
                {
                    board[coord.x + coord.y * BoardWidth] = color;
                }
            }

            merging = true;
            mergeY = BoardHeight - 1;
            mergeTime = Console.FrameCounter;
            mergeCombo = 0;

            moves.Add(new PieceMove(currentPiece, currentX, currentY, currentRotation));
        }

        private void ClearBoard()
        {
            for (int i = 0; i < board.Length; i++)
            {
                board[i] = 0;
            }

            score = 0;
            lines = 0;
            currentIndex = 0;

            pieceQueue.Clear();

            GeneratePiece();
        }

        public override void Update(ConsoleInputState input)
        {
            var blitter = Console.Blitter;

            for (int i = 0; i < Console.LayerCount; i++)
            {
                blitter.Reset();
                blitter.SetTargetLayer((byte)i);

                blitter.SetRenderMode(RetroPixelMode.Depth);
                blitter.Clear(0);

                blitter.SetRenderMode(RetroPixelMode.Brightness);
                blitter.Clear(128);

                blitter.SetRenderMode(RetroPixelMode.Color);
                blitter.Clear((byte)(i == 0 ? 5 : 0));
            }

            switch (menuState)
            {
                case MenuState.Error:
                    {
                        blitter.Reset();

                        int textPos = 8;
                        byte textColor = 1;

                        blitter.EnableDropShadow(RetroDropShadowFlags.Right | RetroDropShadowFlags.Down, 2);
                        blitter.DrawText(textPos, 4, "ERROR", fontTileset, textColor);
                        blitter.DrawText(textPos, 16, errorMsg, fontTileset, textColor);
                        blitter.DisableDropShadow();
                    }
                    break;

                case MenuState.Wait:
                    {
                        blitter.Reset();

                        int textPos = 8;
                        byte textColor = 1;

                        blitter.EnableDropShadow(RetroDropShadowFlags.Right | RetroDropShadowFlags.Down, 2);
                        blitter.DrawText(textPos, 4, "Waiting for wallet...", fontTileset, textColor);
                        blitter.DisableDropShadow();
                    }
                    break;

                case MenuState.Boards:
                    {
                        currentBoard = boardList[boardIndex];

                        blitter.Reset();

                        int textPos = 8;
                        int lineSkip = 12;
                        byte textColor = 1;

                        blitter.DrawLine(4, lineSkip + 2, Console.ResolutionX - 4, lineSkip + 2, 4);

                        blitter.EnableDropShadow(RetroDropShadowFlags.Right | RetroDropShadowFlags.Down, 2);
                        for (int i = 0; i < 3; i++)
                        {
                            string caption;
                            string value;

                            switch (i)
                            {
                                case 0:
                                    caption = "Board";
                                    value = $"\"{currentBoard.Name}\"";
                                    break;

                                case 1:
                                    caption = "Cost";
                                    value = $"{UnitConversion.ToDecimal(currentBoard.Cost, currentBoard.Decimals)} {currentBoard.Symbol}";
                                    break;

                                case 2:
                                    caption = "Pot";
                                    value = $"{UnitConversion.ToDecimal(currentBoard.Pot, currentBoard.Decimals)} {currentBoard.Symbol}";
                                    break;

                                default:
                                    caption = "???";
                                    value = "";
                                    break;
                            }

                            blitter.DrawText(textPos, lineSkip * i, caption, fontTileset, textColor);
                            blitter.DrawText(textPos + 100, lineSkip * i, value, fontTileset, textColor);
                        }

                        blitter.DisableDropShadow();

                        //blitter.DrawTile(80, 10, iconTileset, 0);

                        if (input.pressed.HasFlag(ConsoleInputFlags.Left))
                        {
                            boardIndex--;
                            if (boardIndex < 0)
                            {
                                boardIndex = boardList.Count - 1;
                            }
                        }
                        else
                        if (input.pressed.HasFlag(ConsoleInputFlags.Right))
                        {
                            boardIndex++;
                            if (boardIndex >= boardList.Count)
                            {
                                boardIndex = 0;
                            }
                        }
                        else
                        if (input.pressed.HasFlag(ConsoleInputFlags.A))
                        {
                            menuState = MenuState.Wait;

                            var address = wallet.GetAddress();
                            var script = new ScriptBuilder().
                                AllowGas(address, Address.Null, 100000, 999).
                                CallContract("tetros", "StartGame", currentBoard.Name).
                                SpendGas(address).
                                EndScript();
                            wallet.ExecuteTransaction($"Pay {UnitConversion.ToDecimal(currentBoard.Cost, currentBoard.Decimals)} {currentBoard.Symbol} to start match", script, "lunar", (hash) =>
                            {
                                if (hash != Hash.Null)
                                {
                                    menuState = MenuState.Game;
                                    ClearBoard();
                                }
                                else
                                {
                                    PushError("Could not pay :(");
                                }
                            });
                            return;
                        }

                        break;
                    }

                case MenuState.Game:
                    {
                        int boardOfsX = 4;
                        int boardOfsY = PieceSize;

                        var currentSpeed = 1 + lines / 100;
                        if (currentSpeed > MaxSpeed)
                        {
                            currentSpeed = MaxSpeed;
                        }

                        // draw board
                        blitter.Reset();
                        byte borderColor = 2;
                        var boardTop = boardOfsY;
                        var boardBottom = boardOfsY + BoardHeight * PieceSize;
                        var boardLeft = boardOfsX;
                        var boardRight = boardOfsX + BoardWidth * PieceSize;
                        blitter.DrawLine(boardLeft, boardBottom, boardRight, boardBottom, borderColor);
                        blitter.DrawLine(boardLeft, boardTop, boardLeft, boardBottom, borderColor);
                        blitter.DrawLine(boardRight, boardTop, boardRight, boardBottom, borderColor);

                        for (int j = 0; j < BoardHeight; j++)
                        {
                            for (int i = 0; i < BoardWidth; i++)
                            {
                                var color = board[i + j * BoardWidth];
                                if (color > 0)
                                {
                                    blitter.DrawTile(boardOfsX + i * PieceSize, boardOfsY + j * PieceSize, blockTileset, gameover ? 0 : color);
                                }
                            }
                        }

                        if (!gameover)
                        {
                            if (merging)
                            {
                                var nextTime = mergeTime + 5;

                                if (Console.FrameCounter >= nextTime)
                                {
                                    do
                                    {
                                        bool full = true;

                                        for (int i = 0; i < BoardWidth; i++)
                                        {
                                            var offset = i + mergeY * BoardWidth;
                                            if (board[offset] == 0)
                                            {
                                                full = false;
                                                break;
                                            }
                                        }

                                        if (full)
                                        {
                                            var offset = (BoardWidth - 1) + mergeY * BoardWidth;

                                            while (offset > BoardWidth)
                                            {
                                                board[offset] = board[offset - BoardWidth];
                                                offset--;
                                            }

                                            mergeCombo++;
                                            break;
                                        }
                                        else
                                        {
                                            mergeY--;
                                        }

                                    } while (mergeY > 0);

                                    if (mergeY <= 0)
                                    {
                                        merging = false;

                                        if (mergeCombo > 0)
                                        {
                                            int bonus;

                                            switch (mergeCombo)
                                            {
                                                case 1:
                                                    bonus = 40;
                                                    break;

                                                case 2:
                                                    bonus = 100;
                                                    break;

                                                case 3:
                                                    bonus = 300;
                                                    break;

                                                default:
                                                    bonus = 1200;
                                                    break;
                                            }

                                            var dropDistance = dropping ? currentY - dropY : 0;
                                            bonus += dropDistance;

                                            score += bonus;
                                            lines += mergeCombo;
                                        }

                                        GeneratePiece();
                                    }
                                    else
                                    {
                                        mergeTime = Console.FrameCounter;
                                    }
                                }
                            }
                            else
                            {
                                int dropSpeed = dropping ? 0 : (MaxSpeed - currentSpeed);
                                var nextTime = currentTime + dropSpeed;
                                if (Console.FrameCounter >= nextTime)
                                {
                                    currentTime = Console.FrameCounter;

                                    if (HasCollision(0, 1))
                                    {
                                        MergePiece();
                                    }
                                    else
                                    {
                                        currentY++;
                                    }
                                }

                                var coords = PieceUtils.GetCoords(currentPiece, currentRotation);

                                var delta = (Console.FrameCounter - spawnTime) / 16f;
                                byte alpha;
                                if (delta > 1)
                                {
                                    alpha = 255;
                                }
                                else
                                {
                                    alpha = (byte)(delta * 255);
                                }

                                // draw falling piece
                                blitter.Reset();
                                for (int i = 0; i < coords.Length; i++)
                                {
                                    var coord = coords[i];
                                    coord.x += currentX;
                                    coord.y += currentY;
                                    blitter.DrawTile(boardOfsX + coord.x * PieceSize, boardOfsY + coord.y * PieceSize, blockTileset, 1 + currentIndex % 7, RetroTransform.None, alpha);
                                }
                            }
                        }

                        if (!gameover)
                        {
                            if (!dropping && input.pressed.HasFlag(ConsoleInputFlags.Left))
                            {
                                if (!HasCollision(-1, 0))
                                {
                                    currentX--;
                                }
                            }
                            else
                            if (!dropping && input.pressed.HasFlag(ConsoleInputFlags.Right))
                            {
                                if (!HasCollision(1, 0))
                                {
                                    currentX++;
                                }
                            }
                            else
                            if (!dropping && input.pressed.HasFlag(ConsoleInputFlags.Down))
                            {
                                dropping = true;
                                dropY = currentY;
                            }
                            else
                            if (input.pressed.HasFlag(ConsoleInputFlags.Up))
                            {
                                var prevRot = currentRotation;

                                int maxRotation;

                                switch (currentPiece)
                                {
                                    case PieceKind.TrioL:
                                        maxRotation = 3;
                                        break;

                                    case PieceKind.TetraO:
                                        maxRotation = 0;
                                        break;

                                    default:
                                        maxRotation = 4;
                                        break;
                                }

                                currentRotation++;
                                if (currentRotation >= maxRotation)
                                {
                                    currentRotation = 0;
                                }

                                if (HasCollision(0, 0))
                                {
                                    currentRotation = prevRot;
                                }
                            }
                        }
                        else
                        {
                            if (input.pressed.HasFlag(ConsoleInputFlags.A))
                            {
                                menuState = MenuState.Boards;
                            }
                        }

                        var textPos = boardRight + 4;

                        DrawTextLine(textPos, 0, "Score", score);
                        DrawTextLine(textPos, 1, "Lines", lines);
                        DrawTextLine(textPos, 2, "Speed", currentSpeed);

                        break;
                    }

            }
        }

        private void DrawTextLine(int textPos, int line, string caption, object val)
        {
            byte textColor = 1;
            var lineSkip = 12;
            var linePad = 4;

            var blitter = Console.Blitter;

            line *= 2;

            int baseY = (lineSkip + linePad) * line;

            blitter.EnableDropShadow(RetroDropShadowFlags.Right | RetroDropShadowFlags.Down, 2);
            blitter.DrawText(textPos, baseY, caption, fontTileset, textColor);
            blitter.DrawText(textPos, baseY + lineSkip, val.ToString(), fontTileset, textColor);

            blitter.DisableDropShadow();
            int lineY = baseY + lineSkip * 2 + 2;
            blitter.DrawLine(textPos, lineY, Console.ResolutionX - 4, lineY, 4);
        }
    }
}
