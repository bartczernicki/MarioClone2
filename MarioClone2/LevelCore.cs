using System.Drawing;

namespace MarioClone2;

// Runtime representation of a fully parsed level with entities and collision tiles.
internal sealed class LevelRuntime
{
    public string Name { get; }
    public TileCell[,] Tiles { get; }
    public List<Enemy> Enemies { get; }
    public List<CoinPickup> Coins { get; }
    public List<PowerupPickup> Powerups { get; }
    public PointF Spawn { get; }
    public float FlagX { get; }

    public int Width => Tiles.GetLength(0);
    public int Height => Tiles.GetLength(1);
    public int PixelWidth => Width * GameConstants.TileSize;
    public int PixelHeight => Height * GameConstants.TileSize;

    // Builds a playable level object from parsed tile/entity data.
    public LevelRuntime(
        string name,
        TileCell[,] tiles,
        List<Enemy> enemies,
        List<CoinPickup> coins,
        List<PowerupPickup> powerups,
        PointF spawn,
        float flagX)
    {
        Name = name;
        Tiles = tiles;
        Enemies = enemies;
        Coins = coins;
        Powerups = powerups;
        Spawn = spawn;
        FlagX = flagX;
    }

    // Returns whether a tile coordinate is valid inside the level grid.
    public bool InBounds(int tx, int ty)
    {
        return tx >= 0 && ty >= 0 && tx < Width && ty < Height;
    }

    // Determines if a tile should block movement; out-of-bounds side/bottom are solid.
    public bool IsSolid(int tx, int ty)
    {
        if (tx < 0 || tx >= Width)
        {
            return true;
        }

        if (ty < 0)
        {
            return false;
        }

        if (ty >= Height)
        {
            return true;
        }

        var type = Tiles[tx, ty].Type;
        return type is TileType.Ground or TileType.Brick or TileType.Question or TileType.Pipe or TileType.Spike;
    }

    // Finds the first solid tile under the flagpole for pole rendering.
    public int GroundYAtFlag()
    {
        var tx = Math.Clamp((int)(FlagX / GameConstants.TileSize), 0, Width - 1);
        for (var y = 0; y < Height; y++)
        {
            if (IsSolid(tx, y))
            {
                return y * GameConstants.TileSize;
            }
        }

        return PixelHeight - GameConstants.TileSize;
    }
}

// Authoring format for handcrafted text levels before runtime parsing.
internal sealed class LevelDefinition
{
    public string Name { get; }
    public string[] Rows { get; }

    // Stores a named tilemap using row strings.
    public LevelDefinition(string name, string[] rows)
    {
        Name = name;
        Rows = rows;
    }
}

// Helper for constructing large level layouts with rectangular tile operations.
internal sealed class LevelBuilder
{
    private readonly int _width;
    private readonly int _height;
    private readonly char[,] _cells;

    // Initializes an empty level canvas filled with '.' cells.
    public LevelBuilder(int width, int height)
    {
        _width = width;
        _height = height;
        _cells = new char[width, height];
        FillRect(0, 0, width, height, '.');
    }

    // Fills a full row with a single tile character.
    public void FillRow(int y, char tile)
    {
        FillRect(0, y, _width, 1, tile);
    }

    // Fills an axis-aligned rectangle, clipping to the level bounds.
    public void FillRect(int x, int y, int width, int height, char tile)
    {
        for (var yy = y; yy < y + height; yy++)
        {
            if (yy < 0 || yy >= _height)
            {
                continue;
            }

            for (var xx = x; xx < x + width; xx++)
            {
                if (xx < 0 || xx >= _width)
                {
                    continue;
                }

                _cells[xx, yy] = tile;
            }
        }
    }

    // Clears a rectangle back to empty '.' cells.
    public void ClearRect(int x, int y, int width, int height)
    {
        FillRect(x, y, width, height, '.');
    }

    // Sets one tile if the coordinate is within the level bounds.
    public void Place(int x, int y, char tile)
    {
        if (x < 0 || y < 0 || x >= _width || y >= _height)
        {
            return;
        }

        _cells[x, y] = tile;
    }

    // Converts the mutable char grid into immutable row strings.
    public LevelDefinition Build(string name)
    {
        var rows = new string[_height];
        for (var y = 0; y < _height; y++)
        {
            var chars = new char[_width];
            for (var x = 0; x < _width; x++)
            {
                chars[x] = _cells[x, y];
            }

            rows[y] = new string(chars);
        }

        return new LevelDefinition(name, rows);
    }
}
