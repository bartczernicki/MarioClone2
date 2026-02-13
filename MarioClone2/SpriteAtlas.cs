using System.Drawing;

namespace MarioClone2;

// Creates and owns all procedural sprite/tile bitmaps used by the game.
// Centralizing generation keeps art style consistent and avoids asset files.
internal sealed class SpriteAtlas : IDisposable
{
    public Bitmap MarioIdle { get; }
    public Bitmap MarioIdleBlink { get; }
    public Bitmap MarioRunA { get; }
    public Bitmap MarioRunB { get; }
    public Bitmap MarioRunC { get; }
    public Bitmap MarioJumpUp { get; }
    public Bitmap MarioFall { get; }
    public Bitmap MarioSkid { get; }
    public Bitmap GoombaA { get; }
    public Bitmap GoombaB { get; }
    public Bitmap CoinA { get; }
    public Bitmap CoinB { get; }
    public Bitmap GroundTile { get; }
    public Bitmap BrickTile { get; }
    public Bitmap QuestionTile { get; }
    public Bitmap QuestionUsedTile { get; }
    public Bitmap PipeTile { get; }
    public Bitmap SpikeTile { get; }
    public Bitmap Flag { get; }
    public Bitmap Mushroom { get; }

    // Tracks every allocated bitmap so disposal is guaranteed in one place.
    private readonly List<Bitmap> _all = new();

    public SpriteAtlas()
    {
        // Player frames: idle/blink, three-step run cycle, jump/fall, and skid pose.
        MarioIdle = Add(CreateMarioSprite(0));
        MarioIdleBlink = Add(CreateMarioSprite(6));
        MarioRunA = Add(CreateMarioSprite(1));
        MarioRunB = Add(CreateMarioSprite(2));
        MarioRunC = Add(CreateMarioSprite(7));
        MarioJumpUp = Add(CreateMarioSprite(3));
        MarioFall = Add(CreateMarioSprite(4));
        MarioSkid = Add(CreateMarioSprite(5));

        // Two enemy foot variants alternate to fake walk animation.
        GoombaA = Add(CreateGoombaSprite(false));
        GoombaB = Add(CreateGoombaSprite(true));

        // Wide/thin coin frames alternate to fake coin spin.
        CoinA = Add(CreateCoinSprite(false));
        CoinB = Add(CreateCoinSprite(true));

        // Shared world tiles and level goal marker.
        GroundTile = Add(CreateGroundTile());
        BrickTile = Add(CreateBrickTile());
        QuestionTile = Add(CreateQuestionTile(false));
        QuestionUsedTile = Add(CreateQuestionTile(true));
        PipeTile = Add(CreatePipeTile());
        SpikeTile = Add(CreateSpikeTile());
        Flag = Add(CreateFlagTile());
        Mushroom = Add(CreateMushroomSprite());
    }

    // Chooses player sprite by movement state with extra readability for jump/fall/skid.
    public Bitmap GetPlayerFrame(float animTime, float vx, float vy, bool onGround, int facing)
    {
        if (!onGround)
        {
            return vy < -30f ? MarioJumpUp : MarioFall;
        }

        var speed = MathF.Abs(vx);
        if (speed < 8f)
        {
            var blinkPhase = animTime % 2.8f;
            return blinkPhase >= 2.62f ? MarioIdleBlink : MarioIdle;
        }

        if (speed > 140f && facing != 0 && MathF.Sign(vx) != MathF.Sign(facing))
        {
            return MarioSkid;
        }

        var stepSpeed = Math.Clamp(speed / 240f, 0.58f, 1.2f);
        var frame = (int)(animTime * 13f * stepSpeed) % 3;
        return frame switch
        {
            0 => MarioRunA,
            1 => MarioRunB,
            _ => MarioRunC
        };
    }

    public void Dispose()
    {
        // Dispose every generated bitmap to release native GDI handles.
        foreach (var bmp in _all)
        {
            bmp.Dispose();
        }
    }

    // Registers bitmaps in the disposal list and returns the same instance.
    private Bitmap Add(Bitmap bmp)
    {
        _all.Add(bmp);
        return bmp;
    }

    // Draws a pixel-art-style Mario sprite at 2x scale.
    // `variant` only changes the leg/foot rows to create idle/run/jump poses.
    private static Bitmap CreateMarioSprite(int variant)
    {
        const int scale = 2;
        var bmp = new Bitmap(16 * scale, 16 * scale);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        var hat = Color.FromArgb(205, 42, 33);
        var skin = Color.FromArgb(254, 198, 154);
        var shirt = Color.FromArgb(205, 42, 33);
        var denim = Color.FromArgb(46, 86, 205);
        var hair = Color.FromArgb(84, 49, 20);
        var button = Color.FromArgb(255, 215, 70);
        var eye = Color.FromArgb(30, 30, 30);

        FillPx(g, hat, 3, 1, 10, 3, scale);
        FillPx(g, hair, 3, 4, 3, 3, scale);
        FillPx(g, skin, 6, 4, 6, 4, scale);
        FillPx(g, eye, 8, 5, 1, 1, scale);
        FillPx(g, eye, 10, 5, 1, 1, scale);
        FillPx(g, hair, 8, 7, 4, 1, scale);
        FillPx(g, shirt, 4, 8, 8, 3, scale);
        FillPx(g, denim, 4, 11, 8, 2, scale);
        FillPx(g, button, 6, 11, 1, 1, scale);
        FillPx(g, button, 10, 11, 1, 1, scale);

        switch (variant)
        {
            case 1:
                // Run frame A.
                FillPx(g, denim, 4, 13, 4, 1, scale);
                FillPx(g, denim, 9, 13, 3, 1, scale);
                FillPx(g, hair, 3, 14, 4, 2, scale);
                FillPx(g, hair, 10, 14, 3, 2, scale);
                break;
            case 2:
                // Run frame B.
                FillPx(g, denim, 5, 13, 3, 1, scale);
                FillPx(g, denim, 8, 13, 4, 1, scale);
                FillPx(g, hair, 5, 14, 3, 2, scale);
                FillPx(g, hair, 8, 14, 4, 2, scale);
                break;
            case 3:
                // Jump frame.
                FillPx(g, shirt, 3, 7, 2, 2, scale);
                FillPx(g, shirt, 11, 7, 2, 2, scale);
                FillPx(g, denim, 5, 13, 6, 1, scale);
                FillPx(g, hair, 5, 14, 2, 2, scale);
                FillPx(g, hair, 9, 14, 2, 2, scale);
                break;
            case 4:
                // Fall frame.
                FillPx(g, shirt, 2, 9, 2, 1, scale);
                FillPx(g, shirt, 12, 9, 2, 1, scale);
                FillPx(g, denim, 4, 13, 3, 1, scale);
                FillPx(g, denim, 9, 13, 3, 1, scale);
                FillPx(g, hair, 3, 14, 3, 2, scale);
                FillPx(g, hair, 10, 14, 3, 2, scale);
                break;
            case 5:
                // Skid frame.
                FillPx(g, shirt, 3, 9, 2, 1, scale);
                FillPx(g, denim, 4, 13, 3, 1, scale);
                FillPx(g, denim, 8, 13, 4, 1, scale);
                FillPx(g, hair, 2, 14, 5, 2, scale);
                FillPx(g, hair, 10, 14, 4, 2, scale);
                break;
            case 6:
                // Idle blink frame.
                FillPx(g, denim, 4, 13, 8, 1, scale);
                FillPx(g, hair, 4, 14, 3, 2, scale);
                FillPx(g, hair, 9, 14, 3, 2, scale);
                FillPx(g, skin, 8, 5, 1, 1, scale);
                FillPx(g, skin, 10, 5, 1, 1, scale);
                break;
            case 7:
                // Run frame C.
                FillPx(g, denim, 4, 13, 3, 1, scale);
                FillPx(g, denim, 9, 13, 3, 1, scale);
                FillPx(g, hair, 4, 14, 2, 2, scale);
                FillPx(g, hair, 10, 14, 2, 2, scale);
                break;
            default:
                // Idle frame.
                FillPx(g, denim, 4, 13, 8, 1, scale);
                FillPx(g, hair, 4, 14, 3, 2, scale);
                FillPx(g, hair, 9, 14, 3, 2, scale);
                break;
        }

        return bmp;
    }

    // Draws a Goomba-like enemy sprite.
    // `altFeet` offsets the feet to provide a two-frame walking illusion.
    private static Bitmap CreateGoombaSprite(bool altFeet)
    {
        const int scale = 2;
        var bmp = new Bitmap(16 * scale, 16 * scale);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        var body = Color.FromArgb(115, 74, 39);
        var dark = Color.FromArgb(76, 44, 20);
        var light = Color.FromArgb(229, 202, 160);
        var white = Color.White;
        var black = Color.Black;
        var foot = Color.FromArgb(74, 40, 14);

        FillPx(g, body, 2, 3, 12, 8, scale);
        FillPx(g, dark, 2, 3, 12, 1, scale);
        FillPx(g, light, 5, 6, 6, 2, scale);
        FillPx(g, white, 5, 8, 2, 1, scale);
        FillPx(g, white, 9, 8, 2, 1, scale);
        FillPx(g, black, 6, 8, 1, 1, scale);
        FillPx(g, black, 9, 8, 1, 1, scale);

        if (altFeet)
        {
            FillPx(g, foot, 3, 12, 5, 2, scale);
            FillPx(g, foot, 8, 12, 5, 2, scale);
        }
        else
        {
            FillPx(g, foot, 2, 12, 4, 2, scale);
            FillPx(g, foot, 10, 12, 4, 2, scale);
        }

        return bmp;
    }

    // Draws one coin frame; alternating wide/thin frames implies spinning.
    private static Bitmap CreateCoinSprite(bool thin)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        using var body = new SolidBrush(Color.FromArgb(255, 214, 44));
        using var shade = new SolidBrush(Color.FromArgb(220, 154, 27));

        if (thin)
        {
            g.FillEllipse(body, 6, 1, 4, 14);
            g.FillEllipse(shade, 7, 2, 2, 12);
        }
        else
        {
            g.FillEllipse(body, 2, 1, 12, 14);
            g.FillEllipse(shade, 4, 3, 8, 10);
        }

        return bmp;
    }

    private static Bitmap CreateGroundTile()
    {
        var s = GameConstants.TileSize;
        var bmp = new Bitmap(s, s);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(153, 94, 44));

        using var topBrush = new SolidBrush(Color.FromArgb(196, 140, 70));
        g.FillRectangle(topBrush, 0, 0, s, 6);

        using var crackPen = new Pen(Color.FromArgb(109, 66, 28), 2f);
        for (var y = 9; y < s; y += 8)
        {
            g.DrawLine(crackPen, 0, y, s, y);
        }

        for (var x = 8; x < s; x += 11)
        {
            g.DrawLine(crackPen, x, 10, x - 4, s - 1);
        }

        return bmp;
    }

    private static Bitmap CreateBrickTile()
    {
        var s = GameConstants.TileSize;
        var bmp = new Bitmap(s, s);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(185, 92, 42));

        using var mortar = new Pen(Color.FromArgb(125, 59, 26), 2f);
        g.DrawRectangle(mortar, 1, 1, s - 2, s - 2);
        g.DrawLine(mortar, 0, s / 2, s, s / 2);

        for (var x = s / 4; x < s; x += s / 2)
        {
            g.DrawLine(mortar, x, 0, x, s / 2);
        }

        for (var x = 0; x < s; x += s / 2)
        {
            g.DrawLine(mortar, x, s / 2, x, s);
        }

        return bmp;
    }

    // Draws the "?" block; used blocks switch to a muted palette and hide the mark.
    private static Bitmap CreateQuestionTile(bool used)
    {
        var s = GameConstants.TileSize;
        var bmp = new Bitmap(s, s);
        using var g = Graphics.FromImage(bmp);

        var body = used
            ? Color.FromArgb(165, 130, 85)
            : Color.FromArgb(248, 193, 52);
        var border = used
            ? Color.FromArgb(112, 84, 58)
            : Color.FromArgb(188, 122, 30);

        using var bodyBrush = new SolidBrush(body);
        using var borderPen = new Pen(border, 2f);

        g.FillRectangle(bodyBrush, 0, 0, s, s);
        g.DrawRectangle(borderPen, 1, 1, s - 2, s - 2);

        if (!used)
        {
            using var qBrush = new SolidBrush(Color.FromArgb(150, 82, 18));
            var cx = s / 2;
            g.FillRectangle(qBrush, cx - 3, 8, 6, 4);
            g.FillRectangle(qBrush, cx + 1, 12, 4, 4);
            g.FillRectangle(qBrush, cx - 3, 16, 4, 4);
            g.FillRectangle(qBrush, cx - 3, 24, 4, 4);
        }

        return bmp;
    }

    private static Bitmap CreatePipeTile()
    {
        var s = GameConstants.TileSize;
        var bmp = new Bitmap(s, s);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(62, 168, 77));

        using var shadeBrush = new SolidBrush(Color.FromArgb(47, 128, 58));
        using var lightBrush = new SolidBrush(Color.FromArgb(108, 208, 120));
        using var borderPen = new Pen(Color.FromArgb(39, 102, 46), 2f);

        g.FillRectangle(shadeBrush, s / 2, 0, s / 2, s);
        g.FillRectangle(lightBrush, 3, 3, 6, s - 6);
        g.DrawRectangle(borderPen, 1, 1, s - 2, s - 2);

        return bmp;
    }

    private static Bitmap CreateSpikeTile()
    {
        var s = GameConstants.TileSize;
        var bmp = new Bitmap(s, s);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        using var metal = new SolidBrush(Color.FromArgb(205, 215, 230));
        using var outline = new Pen(Color.FromArgb(118, 126, 140), 2f);
        using var baseBrush = new SolidBrush(Color.FromArgb(95, 103, 116));

        g.FillRectangle(baseBrush, 0, s - 6, s, 6);
        for (var i = 0; i < 4; i++)
        {
            var x = i * (s / 4);
            Point[] spike =
            [
                new Point(x + 2, s - 6),
                new Point(x + (s / 8), 5),
                new Point(x + (s / 4) - 2, s - 6)
            ];

            g.FillPolygon(metal, spike);
            g.DrawPolygon(outline, spike);
        }

        return bmp;
    }

    private static Bitmap CreateFlagTile()
    {
        var bmp = new Bitmap(32, 24);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        using var redBrush = new SolidBrush(Color.FromArgb(214, 50, 50));
        using var whiteBrush = new SolidBrush(Color.FromArgb(245, 245, 245));

        Point[] flagPoints =
        [
            new Point(0, 0),
            new Point(28, 7),
            new Point(0, 14)
        ];

        g.FillPolygon(redBrush, flagPoints);
        g.FillEllipse(whiteBrush, 10, 4, 7, 7);

        return bmp;
    }

    private static Bitmap CreateMushroomSprite()
    {
        const int size = 20;
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);

        using var cap = new SolidBrush(Color.FromArgb(210, 60, 60));
        using var capSpot = new SolidBrush(Color.FromArgb(245, 245, 245));
        using var stem = new SolidBrush(Color.FromArgb(244, 224, 179));
        using var eye = new SolidBrush(Color.FromArgb(44, 44, 44));
        using var outline = new Pen(Color.FromArgb(102, 60, 48), 1f);

        g.FillEllipse(cap, 1, 0, 18, 11);
        g.FillEllipse(capSpot, 4, 2, 4, 3);
        g.FillEllipse(capSpot, 11, 3, 4, 3);

        g.FillRectangle(stem, 4, 8, 12, 10);
        g.FillEllipse(eye, 7, 11, 2, 3);
        g.FillEllipse(eye, 11, 11, 2, 3);

        g.DrawArc(outline, 1, 0, 18, 11, 0, 180);
        g.DrawRectangle(outline, 4, 8, 12, 10);

        return bmp;
    }

    // Draws a single scaled pixel rectangle in sprite-space coordinates.
    private static void FillPx(Graphics g, Color color, int x, int y, int w, int h, int scale)
    {
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, x * scale, y * scale, w * scale, h * scale);
    }
}
