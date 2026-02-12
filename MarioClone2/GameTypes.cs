using System.Drawing;

namespace MarioClone2;

internal static class GameConstants
{
    public const int TileSize = 32;
    public const int ViewWidth = 960;
    public const int ViewHeight = 540;
    public const float Gravity = 1500f;
}

internal enum GamePhase
{
    Playing,
    Won,
    GameOver
}

internal enum TileType
{
    Empty,
    Ground,
    Brick,
    Question,
    Pipe
}

internal sealed class Player
{
    public float X;
    public float Y;
    public float Vx;
    public float Vy;

    public float Width { get; } = 24f;
    public float Height { get; } = 30f;
    public bool OnGround;
    public int Facing = 1;

    public Player(PointF spawn)
    {
        X = spawn.X;
        Y = spawn.Y;
    }
}

internal sealed class Enemy
{
    public float X;
    public float Y;
    public float Vx;
    public float Vy;
    public float Width { get; } = 26f;
    public float Height { get; } = 24f;
    public bool Alive = true;
    public bool OnGround;

    public Enemy(float x, float y, float vx)
    {
        X = x;
        Y = y;
        Vx = vx;
    }
}

internal sealed class CoinPickup
{
    public float X;
    public float Y;
    public bool Collected;
    public float PulseOffset;

    public CoinPickup(float x, float y, float pulseOffset)
    {
        X = x;
        Y = y;
        PulseOffset = pulseOffset;
    }
}

internal sealed class TileCell
{
    public TileType Type;
    public bool Used;

    public TileCell(TileType type)
    {
        Type = type;
    }
}
