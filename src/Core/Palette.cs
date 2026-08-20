using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// A curated, ready-to-use set of vibrant, harmonious colors, each hue paired with a
    /// slightly darker shade (e.g. <see cref="Emerald"/>/<see cref="Nephritis"/>) for a
    /// border, shadow, or pressed state without hand-picking a second color. For prototypes
    /// and demos where the point is the simulation or the gameplay, not a color pass.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Turquoise = new(26, 188, 156);
        public static readonly Color GreenSea = new(22, 160, 133);
        public static readonly Color Emerald = new(46, 204, 113);
        public static readonly Color Nephritis = new(39, 174, 96);
        public static readonly Color PeterRiver = new(52, 152, 219);
        public static readonly Color BelizeHole = new(41, 128, 185);
        public static readonly Color Amethyst = new(155, 89, 182);
        public static readonly Color Wisteria = new(142, 68, 173);
        public static readonly Color WetAsphalt = new(52, 73, 94);
        public static readonly Color MidnightBlue = new(44, 62, 80);
        public static readonly Color Sunflower = new(241, 196, 15);
        public static readonly Color Orange = new(243, 156, 18);
        public static readonly Color Carrot = new(230, 126, 34);
        public static readonly Color Pumpkin = new(211, 84, 0);
        public static readonly Color Alizarin = new(231, 76, 60);
        public static readonly Color Pomegranate = new(192, 57, 43);
        public static readonly Color Clouds = new(236, 240, 241);
        public static readonly Color Silver = new(189, 195, 199);
        public static readonly Color Concrete = new(149, 165, 166);
        public static readonly Color Asbestos = new(127, 140, 141);

        /// <summary>Near-black charcoal-navy — a whole-screen backdrop for a dark dashboard/HUD, not a content color. <see cref="MidnightBlue"/>/<see cref="WetAsphalt"/> work well as panels on top of it.</summary>
        public static readonly Color Background = new(20, 22, 31);

        /// <summary>All 21 colors above — for picking one at random (e.g. a color per boid/cell/agent).</summary>
        public static readonly Color[] All =
        {
            Turquoise, GreenSea, Emerald, Nephritis, PeterRiver, BelizeHole, Amethyst, Wisteria,
            WetAsphalt, MidnightBlue, Sunflower, Orange, Carrot, Pumpkin, Alizarin, Pomegranate,
            Clouds, Silver, Concrete, Asbestos, Background
        };

        /// <summary>The 10 brighter/primary hues only (skipping each one's darker pair) — visually distinct even in a short sequence.</summary>
        public static readonly Color[] Primary =
        {
            Turquoise, Emerald, PeterRiver, Amethyst, WetAsphalt, Sunflower, Carrot, Alizarin, Clouds, Concrete
        };

        /// <summary>A color from <see cref="Primary"/>, cycling by index — deterministic (same index always gives the same color), e.g. one color per simulation category.</summary>
        public static Color Cycle(int index)
        {
            int i = index % Primary.Length;
            if (i < 0) i += Primary.Length;
            return Primary[i];
        }
    }
}
