using Microsoft.Xna.Framework;

namespace MonoPrimitives
{
    /// <summary>
    /// A curated, ready-to-use set of vibrant, harmonious colors, each hue paired with a
    /// slightly darker shade (e.g. <see cref="Emerald"/>/<see cref="Nephritis"/>) for a
    /// border, shadow, or pressed state without hand-picking a second color.
    /// </summary>
    /// <remarks>For prototypes and demos where the point is the simulation or the gameplay, not a color pass.</remarks>
    public static class Palette
    {
        /// <summary>Bright teal — green and cyan meeting halfway, like shallow tropical water.</summary>
        public static readonly Color Turquoise = new(26, 188, 156);

        /// <summary><see cref="Turquoise"/>'s deeper twin — a moody, overcast-ocean teal.</summary>
        public static readonly Color GreenSea = new(22, 160, 133);

        /// <summary>A vivid grass green — the classic "go"/"success" green.</summary>
        public static readonly Color Emerald = new(46, 204, 113);

        /// <summary><see cref="Emerald"/>'s shadow — a deep forest green.</summary>
        public static readonly Color Nephritis = new(39, 174, 96);

        /// <summary>A clear, friendly sky blue — bright, not navy.</summary>
        public static readonly Color PeterRiver = new(52, 152, 219);

        /// <summary><see cref="PeterRiver"/>'s deeper twin — a rich ocean blue.</summary>
        public static readonly Color BelizeHole = new(41, 128, 185);

        /// <summary>A soft violet-purple, like the gemstone it's named for.</summary>
        public static readonly Color Amethyst = new(155, 89, 182);

        /// <summary><see cref="Amethyst"/>'s deeper shade — a rich grape purple.</summary>
        public static readonly Color Wisteria = new(142, 68, 173);

        /// <summary>A dark, cool slate blue-gray — wet pavement under streetlight.</summary>
        public static readonly Color WetAsphalt = new(52, 73, 94);

        /// <summary>Darker still than <see cref="WetAsphalt"/> — near-black with a blue undertone.</summary>
        public static readonly Color MidnightBlue = new(44, 62, 80);

        /// <summary>A warm golden yellow, like a sunflower's petals.</summary>
        public static readonly Color Sunflower = new(241, 196, 15);

        /// <summary>A bright, classic orange.</summary>
        public static readonly Color Orange = new(243, 156, 18);

        /// <summary>A deeper, more muted orange — a carrot's skin rather than a traffic cone.</summary>
        public static readonly Color Carrot = new(230, 126, 34);

        /// <summary>A burnt orange edging toward red-brown, like a pumpkin's rind.</summary>
        public static readonly Color Pumpkin = new(211, 84, 0);

        /// <summary>A warm red leaning slightly orange — a ripe tomato.</summary>
        public static readonly Color Alizarin = new(231, 76, 60);

        /// <summary><see cref="Alizarin"/>'s deeper shade — a brick-red pomegranate skin.</summary>
        public static readonly Color Pomegranate = new(192, 57, 43);

        /// <summary>Almost white — a soft, cool off-white like an overcast sky.</summary>
        public static readonly Color Clouds = new(236, 240, 241);

        /// <summary>A light, cool gray — brushed silver.</summary>
        public static readonly Color Silver = new(189, 195, 199);

        /// <summary>A medium gray with a faint blue-green cast, like poured concrete.</summary>
        public static readonly Color Concrete = new(149, 165, 166);

        /// <summary><see cref="Concrete"/>'s darker, more muted twin — a dim slate gray.</summary>
        public static readonly Color Asbestos = new(127, 140, 141);

        /// <summary>Near-black charcoal-navy — a whole-screen backdrop for a dark dashboard/HUD, not a content color. <see cref="MidnightBlue"/>/<see cref="WetAsphalt"/> work well as panels on top of it.</summary>
        public static readonly Color Background = new(20, 22, 31);

        /// <summary>
        /// All 21 colors above, <see cref="Background"/> included — for code that genuinely
        /// wants every curated color (a palette swatch viewer, a "cycle through all of them"
        /// debug tool).
        /// </summary>
        /// <remarks>
        /// For picking a random FOREGROUND color (a boid/cell/agent), use
        /// <see cref="Primary"/> or <see cref="Cycle"/> instead: <see cref="Background"/> is a
        /// near-black backdrop color, and a random pick from this array can silently return it,
        /// rendering as invisible/near-invisible against the very background it's meant for.
        /// </remarks>
        public static readonly Color[] All =
        {
            Turquoise, GreenSea, Emerald, Nephritis, PeterRiver, BelizeHole, Amethyst, Wisteria,
            WetAsphalt, MidnightBlue, Sunflower, Orange, Carrot, Pumpkin, Alizarin, Pomegranate,
            Clouds, Silver, Concrete, Asbestos, Background
        };

        /// <summary>The 10 primary hues only (skipping each one's darker pair, and never <see cref="Background"/>) — visually distinct even in a short sequence, and always safe to use as a foreground color.</summary>
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

        // ---------------------------------------------------------------------
        // Gradient pairs — a different flavor from the flat colors above: an inner
        // (highlight) / outer (edge) pair per entry, for the glossy, saturated,
        // "juicy" toy-ball look.
        // Feed a pair straight into FillCircleGradient/FillCircleGradientLinear.
        // A curated subset, not exhaustive — add more pairs here as needed rather than
        // building a separate one-off array elsewhere.
        // ---------------------------------------------------------------------

        /// <summary>Paired inner (highlight)/outer (edge) colors for a glossy radial-gradient ball — e.g. <c>batch.FillCircleGradient(center, radius, GradientPairs[i].Inner, GradientPairs[i].Outer)</c>.</summary>
        public static readonly (Color Inner, Color Outer)[] GradientPairs =
        {
            (new Color(255, 120, 140), new Color(215, 20, 45)),   // Cherry — red
            (new Color(130, 180, 255), new Color(30, 90, 220)),   // Grape — blue
            (new Color(255, 210, 100), new Color(245, 130, 15)),  // Dekopon — orange
            (new Color(230, 100, 210), new Color(160, 30, 130)),  // Plum — magenta
            (new Color(255, 240, 130), new Color(245, 175, 15)),  // Pineapple — yellow
            (new Color(90, 230, 90), new Color(10, 120, 40)),     // Watermelon — green
        };
    }
}
