using System.Drawing;

namespace MarioClone2;

// Shared game-wide constants for physics, tile size, and viewport dimensions.
internal static class GameConstants
{
    public const int TileSize = 32;
    public const int ViewWidth = 960;
    public const int ViewHeight = 540;
    public const float Gravity = 1500f;
}

internal enum GamePhase
{
    // Normal gameplay update/render loop.
    Playing,
    // Player reached the level goal.
    Won,
    // Player died or failed the level.
    GameOver
}

// Tile semantics used in authored row strings and runtime collision.
internal enum TileType
{
    Empty,
    Ground,
    Brick,
    Question,
    Pipe
}

// Mutable player state used by simulation and rendering.
internal sealed class Player
{
    public float X;
    public float Y;
    public float Vx;
    public float Vy;

    public float Width { get; } = 24f;
    public float Height { get; } = 30f;
    public bool OnGround;
    // Horizontal facing direction: -1 left, +1 right.
    public int Facing = 1;

    // Initializes the player at the current level spawn point.
    public Player(PointF spawn)
    {
        X = spawn.X;
        Y = spawn.Y;
    }
}

// Mutable enemy state for simple patrolling and collision behavior.
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

    // Spawns a walking enemy at the requested world position and speed.
    public Enemy(float x, float y, float vx)
    {
        X = x;
        Y = y;
        Vx = vx;
    }
}

// Collectible coin state, including a per-coin animation phase offset.
internal sealed class CoinPickup
{
    public float X;
    public float Y;
    public bool Collected;
    // De-syncs coin pulse animations so rows do not blink in lockstep.
    public float PulseOffset;

    // Creates a coin pickup in world space.
    public CoinPickup(float x, float y, float pulseOffset)
    {
        X = x;
        Y = y;
        PulseOffset = pulseOffset;
    }
}

// Runtime tile cell data with optional per-tile state (for used question blocks).
internal sealed class TileCell
{
    public TileType Type;
    public bool Used;

    // Creates a tile cell from a static level tile type.
    public TileCell(TileType type)
    {
        Type = type;
    }
}
