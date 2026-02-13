using System.Drawing;

namespace MarioClone2;

// Builds authored level definitions and converts them into runtime data.
internal static class LevelFactory
{
    // Returns the full ordered campaign list.
    public static List<LevelDefinition> Create()
    {
        return
        [
            CreateGreenHills(),
            CreateSkyFortress()
        ];
    }

    // Parses tile characters into collision tiles, entities, spawn, and flag data.
    public static LevelRuntime CreateRuntime(LevelDefinition definition)
    {
        var height = definition.Rows.Length;
        var width = definition.Rows.Max(row => row.Length);

        var tiles = new TileCell[width, height];
        var enemies = new List<Enemy>();
        var coins = new List<CoinPickup>();
        var powerups = new List<PowerupPickup>();

        // Fallback values used if a map omits explicit spawn/flag markers.
        var spawn = new PointF(64f, 64f);
        var flagX = (width - 4) * GameConstants.TileSize + 0f;

        for (var y = 0; y < height; y++)
        {
            var row = definition.Rows[y];
            for (var x = 0; x < width; x++)
            {
                // Treat missing columns in short rows as empty space.
                var c = x < row.Length ? row[x] : '.';
                var tileType = TileType.Empty;

                // Authoring legend:
                // # ground, B brick, ? question, P pipe, ^ spike,
                // M spawn, E enemy, C coin, U power-up, F flag.
                switch (c)
                {
                    case '#':
                        tileType = TileType.Ground;
                        break;
                    case 'B':
                        tileType = TileType.Brick;
                        break;
                    case '?':
                        tileType = TileType.Question;
                        break;
                    case 'P':
                        tileType = TileType.Pipe;
                        break;
                    case '^':
                        tileType = TileType.Spike;
                        break;
                    case 'M':
                        spawn = new PointF(x * GameConstants.TileSize + 4f, y * GameConstants.TileSize + 2f);
                        break;
                    case 'E':
                        enemies.Add(new Enemy(
                            x * GameConstants.TileSize + 3f,
                            y * GameConstants.TileSize + 8f,
                            -75f));
                        break;
                    case 'C':
                        coins.Add(new CoinPickup(
                            x * GameConstants.TileSize + 8f,
                            y * GameConstants.TileSize + 8f,
                            (x + y) * 0.42f));
                        break;
                    case 'U':
                        powerups.Add(new PowerupPickup(
                            x * GameConstants.TileSize + 6f,
                            y * GameConstants.TileSize + 8f));
                        break;
                    case 'F':
                        flagX = x * GameConstants.TileSize + 8f;
                        break;
                }

                tiles[x, y] = new TileCell(tileType);
            }
        }

        return new LevelRuntime(definition.Name, tiles, enemies, coins, powerups, spawn, flagX);
    }

    // World 1 layout with gentle platforming and enemy density.
    private static LevelDefinition CreateGreenHills()
    {
        var b = new LevelBuilder(220, 17);

        b.FillRow(15, '#');
        b.FillRow(16, '#');
        b.Place(2, 13, 'M');
        b.Place(212, 8, 'F');

        b.ClearRect(28, 15, 4, 2);
        b.ClearRect(74, 15, 4, 2);
        b.ClearRect(123, 15, 5, 2);
        b.ClearRect(171, 15, 4, 2);

        b.FillRect(40, 13, 2, 2, 'P');
        b.FillRect(92, 12, 2, 3, 'P');
        b.FillRect(150, 13, 2, 2, 'P');

        b.FillRect(16, 11, 6, 1, 'B');
        b.Place(17, 11, '?');
        b.Place(19, 11, '?');

        b.FillRect(52, 10, 4, 1, 'B');
        b.Place(53, 10, '?');
        b.Place(55, 10, '?');

        b.FillRect(80, 9, 6, 1, 'B');
        b.Place(81, 9, '?');
        b.Place(84, 9, '?');

        b.FillRect(134, 11, 6, 1, 'B');
        b.Place(135, 11, '?');
        b.Place(138, 11, '?');

        b.Place(12, 12, 'U');
        b.Place(76, 11, 'U');
        b.Place(168, 12, 'U');

        b.Place(34, 14, '^');
        b.Place(68, 14, '^');
        b.Place(118, 14, '^');
        b.Place(166, 14, '^');

        for (var step = 0; step < 6; step++)
        {
            b.FillRect(198 + step, 14 - step, 1, step + 1, 'B');
        }

        for (var x = 10; x <= 24; x += 2)
        {
            b.Place(x, 8, 'C');
        }

        for (var x = 50; x <= 58; x += 2)
        {
            b.Place(x, 7, 'C');
        }

        for (var x = 80; x <= 90; x += 2)
        {
            b.Place(x, 6, 'C');
        }

        for (var x = 132; x <= 144; x += 2)
        {
            b.Place(x, 8, 'C');
        }

        for (var x = 176; x <= 192; x += 2)
        {
            b.Place(x, 7, 'C');
        }

        foreach (var x in new[] { 20, 36, 60, 88, 104, 142, 160, 184 })
        {
            b.Place(x, 14, 'E');
        }

        return b.Build("Green Hills");
    }

    // World 2 layout with wider gaps and denser vertical platforming.
    private static LevelDefinition CreateSkyFortress()
    {
        var b = new LevelBuilder(240, 17);

        b.FillRow(15, '#');
        b.FillRow(16, '#');
        b.Place(2, 13, 'M');
        b.Place(232, 8, 'F');

        b.ClearRect(18, 15, 6, 2);
        b.ClearRect(48, 15, 5, 2);
        b.ClearRect(86, 15, 5, 2);
        b.ClearRect(126, 15, 7, 2);
        b.ClearRect(170, 15, 7, 2);
        b.ClearRect(210, 15, 7, 2);

        b.FillRect(12, 12, 10, 1, 'B');
        b.FillRect(28, 10, 9, 1, 'B');
        b.FillRect(58, 11, 8, 1, 'B');
        b.FillRect(94, 9, 10, 1, 'B');
        b.FillRect(140, 10, 9, 1, 'B');
        b.FillRect(182, 9, 10, 1, 'B');

        b.Place(15, 12, '?');
        b.Place(31, 10, '?');
        b.Place(62, 11, '?');
        b.Place(98, 9, '?');
        b.Place(145, 10, '?');
        b.Place(188, 9, '?');

        b.Place(24, 9, 'U');
        b.Place(120, 10, 'U');
        b.Place(196, 8, 'U');

        b.Place(30, 14, '^');
        b.Place(62, 14, '^');
        b.Place(100, 14, '^');
        b.Place(146, 14, '^');
        b.Place(188, 14, '^');

        b.FillRect(40, 13, 2, 2, 'P');
        b.FillRect(76, 12, 2, 3, 'P');
        b.FillRect(116, 13, 2, 2, 'P');
        b.FillRect(160, 12, 2, 3, 'P');

        for (var step = 0; step < 7; step++)
        {
            b.FillRect(218 + step, 14 - step, 1, step + 1, 'B');
        }

        for (var x = 12; x <= 34; x += 2)
        {
            b.Place(x, 7, 'C');
        }

        for (var x = 58; x <= 76; x += 2)
        {
            b.Place(x, 7, 'C');
        }

        for (var x = 94; x <= 112; x += 2)
        {
            b.Place(x, 6, 'C');
        }

        for (var x = 140; x <= 156; x += 2)
        {
            b.Place(x, 7, 'C');
        }

        for (var x = 182; x <= 204; x += 2)
        {
            b.Place(x, 6, 'C');
        }

        foreach (var x in new[] { 10, 26, 44, 72, 106, 132, 154, 176, 198, 224 })
        {
            b.Place(x, 14, 'E');
        }

        return b.Build("Sky Fortress");
    }
}
