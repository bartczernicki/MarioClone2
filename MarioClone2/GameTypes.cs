using System.Drawing;

namespace MarioClone2;

// Shared game-wide constants for physics, tile size, and viewport dimensions.
internal static class GameConstants
{
    public const int TileSize = 32;
    public const int ViewWidth = 960;
    public const int ViewHeight = 540;
    public const float Gravity = 1500f;
    public const float CoyoteTimeSeconds = 0.10f;
    public const float JumpBufferSeconds = 0.12f;
    public const float WalkMaxRunSpeed = 240f;
    public const float SprintMaxRunSpeed = 320f;
    public const float WalkAcceleration = 1600f;
    public const float SprintAcceleration = 1950f;
    public const float GroundFriction = 2400f;
    public const float AirDrag = 380f;
    // Native pixel dimensions of generated coin sprites in the atlas.
    public const float CoinSourceSize = 16f;
    // On-screen coin size after upscale for better readability.
    public const float CoinRenderSize = 22f;
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
    Pipe,
    Spike
}

internal enum PlayerPowerState
{
    // Default one-hit state.
    Small,
    // Expanded state; first non-stomp hit shrinks instead of consuming a life.
    Big
}

// Mutable player state used by simulation and rendering.
internal sealed class Player
{
    public float X;
    public float Y;
    public float Vx;
    public float Vy;

    public const float SmallHeight = 30f;
    public const float BigHeight = 42f;

    public float Width { get; } = 24f;
    public float Height => PowerState == PlayerPowerState.Big ? BigHeight : SmallHeight;
    public bool OnGround;
    // Horizontal facing direction: -1 left, +1 right.
    public int Facing = 1;
    public PlayerPowerState PowerState = PlayerPowerState.Small;
    // Brief invulnerability window after shrink/hit to prevent rapid chained damage.
    public float DamageCooldownSeconds;
    public float CoyoteTimerSeconds;
    public float JumpBufferTimerSeconds;
    public bool Sprinting;

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

internal sealed class PowerupPickup
{
    public float X;
    public float Y;
    // Set once collected so the pickup is no longer rendered/collidable.
    public bool Collected;

    // Creates a mushroom pickup in world space.
    public PowerupPickup(float x, float y)
    {
        X = x;
        Y = y;
    }
}

internal sealed class CheckpointMarker
{
    public float X;
    public float Y;
    public bool Activated;
    public int OrderIndex;

    public CheckpointMarker(float x, float y, int orderIndex)
    {
        X = x;
        Y = y;
        OrderIndex = orderIndex;
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
