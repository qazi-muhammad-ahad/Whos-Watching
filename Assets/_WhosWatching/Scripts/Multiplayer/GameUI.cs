using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WhosWatching
{
    // Console-quality cartoon IMGUI theme matching the game's bold art direction.
    public static class GameUI
    {
        // ──── Colour palette ────
        public static readonly Color Ink        = new(0.02f, 0.03f, 0.06f);
        public static readonly Color Cream      = new(0.98f, 0.97f, 0.91f);
        public static readonly Color Gold       = new(1f, 0.62f, 0.20f);
        public static readonly Color Mint       = new(0.20f, 0.88f, 0.76f);
        public static readonly Color Coral      = new(1f, 0.30f, 0.30f);
        public static readonly Color NeonRed    = new(1f, 0.25f, 0.20f);
        public static readonly Color NeonBlue   = new(0.15f, 0.55f, 1f);
        public static readonly Color HotOrange  = new(1f, 0.45f, 0.12f);
        public static readonly Color SkyBlue    = new(0.25f, 0.65f, 1f);
        public static readonly Color RoyalPurple= new(0.65f, 0.38f, 1f);
        public static readonly Color Panel      = new(0.02f, 0.035f, 0.07f, 0.92f);
        public static readonly Color PanelLight = new(0.04f, 0.07f, 0.12f, 0.90f);
        public static readonly Color PanelDark  = new(0.01f, 0.02f, 0.04f, 0.95f);
        public static readonly Color Muted      = new(0.65f, 0.72f, 0.76f);
        public static readonly Color Line       = new(0.22f, 0.35f, 0.42f, 0.55f);
        public static readonly Color Shadow     = new(0f, 0f, 0f, 0.65f);

        // Button text colour — bold red-orange like the reference image
        public static readonly Color BtnTextColor = new(0.95f, 0.30f, 0.12f);

        static GUISkin skin;
        static Texture2D flat;
        static Texture2D logoTexture;
        static Texture2D titleTexture;
        static Texture2D btnStartTexture;
        static Texture2D btnSettingsTexture;
        static Texture2D btnHowTexture;
        static GUIStyle title, display, heading, small, centered, overline, step, boldLabel;
        static GUIStyle subtitleStyle, tagline, modernInputStyle;
        static bool? details;

        public static bool Details
        {
            get => details ??= PlayerPrefs.GetInt("WW.DetailedHUD", 1) != 0;
            set { details = value; PlayerPrefs.SetInt("WW.DetailedHUD", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float Scale => Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        public static float Width => Screen.width / Mathf.Max(0.1f, Scale);
        public static float Height => Screen.height / Mathf.Max(0.1f, Scale);

        public static GUIStyle Title    => title;
        public static GUIStyle Display  => display;
        public static GUIStyle Heading  => heading;
        public static GUIStyle Small    => small;
        public static GUIStyle Center   => centered;
        public static GUIStyle Overline => overline;
        public static GUIStyle Step     => step;
        public static GUIStyle BoldLabel=> boldLabel;
        public static GUIStyle Subtitle => subtitleStyle;
        public static GUIStyle Tagline  => tagline;

        public static Texture2D TitleScreenTexture      => titleTexture ??= LoadTexture("TitleScreen.jpg", "TitleScreen.png");
        public static Texture2D BtnStartTexture         => btnStartTexture ??= LoadTexture("Btn_StartGame.png");
        public static Texture2D BtnSettingsTexture      => btnSettingsTexture ??= LoadTexture("Btn_Settings.png");
        public static Texture2D BtnHowTexture           => btnHowTexture ??= LoadTexture("Btn_HowToPlay.png");
        public static Texture2D LogoTexture             => logoTexture ??= LoadTexture("Logo_WhosWatching.png");

        static Texture2D LoadTexture(params string[] filenames)
        {
            foreach (var fn in filenames)
            {
                try
                {
                    // 1. Try Resources.Load (Works inside standalone .exe builds & Editor)
                    string resName = Path.GetFileNameWithoutExtension(fn);
                    var resTex = Resources.Load<Texture2D>("UI/" + resName);
                    if (resTex != null) return resTex;

                    // 2. Try StreamingAssets
                    string saPath = Path.Combine(Application.streamingAssetsPath, "UI", fn);
                    if (File.Exists(saPath))
                    {
                        byte[] bytes = File.ReadAllBytes(saPath);
                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        tex.LoadImage(bytes);
                        tex.filterMode = FilterMode.Bilinear;
                        return tex;
                    }

                    // 3. Try Application.dataPath (Editor Assets folder)
                    string path = Path.Combine(Application.dataPath, "_WhosWatching/Art/UI", fn);
                    if (File.Exists(path))
                    {
                        byte[] bytes = File.ReadAllBytes(path);
                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        tex.LoadImage(bytes);
                        tex.filterMode = FilterMode.Bilinear;
                        return tex;
                    }

                    // 4. Try adjacent UI folder beside the .exe
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    string exeNeighborPath = Path.Combine(exeDir, "UI", fn);
                    if (File.Exists(exeNeighborPath))
                    {
                        byte[] bytes = File.ReadAllBytes(exeNeighborPath);
                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        tex.LoadImage(bytes);
                        tex.filterMode = FilterMode.Bilinear;
                        return tex;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("LoadTexture " + fn + ": " + ex.Message);
                }
            }
            return null;
        }

        public static Scope Begin() { Ensure(); return new Scope(false); }

        public struct Scope : IDisposable
        {
            readonly GUISkin oldSkin; readonly Matrix4x4 matrix; readonly Color color, background, content;
            readonly bool enabled; readonly int depth;
            public Scope(bool unused = false)
            {
                oldSkin = GUI.skin; matrix = GUI.matrix; color = GUI.color;
                background = GUI.backgroundColor; content = GUI.contentColor;
                enabled = GUI.enabled; depth = GUI.depth;
                GUI.skin = skin;
                GUI.matrix = Matrix4x4.Scale(Vector3.one * Mathf.Max(0.1f, Scale));
                GUI.color = GUI.backgroundColor = GUI.contentColor = Color.white;
            }
            public void Dispose()
            {
                GUI.skin = oldSkin; GUI.matrix = matrix; GUI.color = color;
                GUI.backgroundColor = background; GUI.contentColor = content;
                GUI.enabled = enabled; GUI.depth = depth;
            }
        }

        static void Ensure()
        {
            if (skin) return;
            flat = Texture2D.whiteTexture;
            skin = UnityEngine.Object.Instantiate(GUI.skin);
            skin.hideFlags = HideFlags.HideAndDontSave;

            skin.label.fontSize = 17;
            skin.label.normal.textColor = Cream;
            skin.label.wordWrap = true;
            skin.label.padding = new RectOffset(0, 0, 4, 4);

            skin.button = new GUIStyle(skin.button)
            {
                fontSize = 17, fontStyle = FontStyle.Bold, fixedHeight = 46,
                margin = new RectOffset(4, 4, 6, 6),
                border = new RectOffset(10, 10, 10, 10),
                alignment = TextAnchor.MiddleCenter
            };
            skin.button.normal.background = flat;  skin.button.normal.textColor  = Ink;
            skin.button.hover.background  = flat;  skin.button.hover.textColor   = Ink;
            skin.button.active.background = flat;  skin.button.active.textColor  = Ink;
            skin.button.onNormal.background = flat; skin.button.onNormal.textColor = Ink;
            skin.button.onHover.background  = flat; skin.button.onHover.textColor  = Ink;
            skin.button.onActive.background = flat; skin.button.onActive.textColor = Ink;
            skin.label.fontSize = 16;
            skin.label.normal.textColor = Cream;
            skin.label.wordWrap = false;
            skin.label.clipping = TextClipping.Overflow;
            skin.label.padding = new RectOffset(0, 0, 0, 0);
            skin.label.margin = new RectOffset(0, 0, 0, 0);

            skin.button = new GUIStyle(skin.button)
            {
                fontSize = 16, fontStyle = FontStyle.Bold, fixedHeight = 44,
                margin = new RectOffset(4, 4, 6, 6),
                border = new RectOffset(10, 10, 10, 10),
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow
            };
            skin.button.normal.background = flat;  skin.button.normal.textColor  = Ink;
            skin.button.hover.background  = flat;  skin.button.hover.textColor   = Ink;
            skin.button.active.background = flat;  skin.button.active.textColor  = Ink;
            skin.button.onNormal.background = flat; skin.button.onNormal.textColor = Ink;
            skin.button.onHover.background  = flat; skin.button.onHover.textColor  = Ink;
            skin.button.onActive.background = flat; skin.button.onActive.textColor = Ink;

            skin.textField = new GUIStyle(skin.textField)
            {
                fontSize = 17, fixedHeight = 38,
                padding = new RectOffset(10, 10, 6, 6),
                border = new RectOffset(6, 6, 6, 6),
                clipping = TextClipping.Overflow
            };
            skin.textField.normal.background = flat; skin.textField.normal.textColor = Cream;
            skin.textField.focused.background = flat; skin.textField.focused.textColor = Cream;

            title    = new GUIStyle(skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            display  = new GUIStyle(skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            heading  = new GUIStyle(skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
            small    = new GUIStyle(skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow }; small.normal.textColor = Muted;
            centered = new GUIStyle(skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            overline = new GUIStyle(skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow }; overline.normal.textColor = Gold;
            step     = new GUIStyle(skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow }; step.normal.textColor = Mint;
            boldLabel = new GUIStyle(skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };

            // Subtitle — large white bold, very readable
            subtitleStyle = new GUIStyle(skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow
            };
            subtitleStyle.normal.textColor = Cream;

            // Footer tagline
            tagline = new GUIStyle(skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow
            };
            tagline.normal.textColor = new Color(0.6f, 0.65f, 0.7f, 0.6f);
        }

        // ──── Drawing helpers ────
        public static void Fill(Rect r, Color c, bool rounded = true)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, flat);
            GUI.color = old;
        }

        public static void Card(Rect r, Color? color = null)
        {
            Fill(new Rect(r.x + 4, r.y + 5, r.width, r.height), Shadow, false);
            Fill(r, color ?? Panel, false);
            Fill(new Rect(r.x, r.y, r.width, 2), new Color(1f, 1f, 1f, 0.10f), false);
        }

        public static void ModernPanel(Rect r, Color accentColor)
        {
            // Drop shadow
            Fill(new Rect(r.x + 5, r.y + 6, r.width, r.height), new Color(0, 0, 0, 0.55f), false);

            // Dark translucent glass body
            Fill(r, new Color(0.035f, 0.055f, 0.095f, 0.92f), false);

            // Top neon accent bar
            Fill(new Rect(r.x, r.y, r.width, 3), accentColor, false);

            // Subtle neon glow under top bar
            var old = GUI.color;
            GUI.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.15f);
            Fill(new Rect(r.x, r.y + 3, r.width, 8), accentColor, false);
            GUI.color = old;

            // Subtle side & bottom border
            Color edgeCol = new Color(accentColor.r, accentColor.g, accentColor.b, 0.25f);
            Fill(new Rect(r.x, r.y + 3, 1, r.height - 3), edgeCol, false);
            Fill(new Rect(r.xMax - 1, r.y + 3, 1, r.height - 3), edgeCol, false);
            Fill(new Rect(r.x, r.yMax - 1, r.width, 1), edgeCol, false);
        }

        public static bool ModernButton(Rect r, string label, Color baseColor, bool isSelected = false)
        {
            bool hovered = r.Contains(MousePosition);
            Color drawCol = isSelected ? baseColor : (hovered ? Color.Lerp(baseColor, Color.white, 0.22f) : baseColor);

            // Button shadow
            Fill(new Rect(r.x + 2, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.45f), false);

            // Button body
            Fill(r, drawCol, false);

            // Highlight top rim
            Fill(new Rect(r.x, r.y, r.width, 2), new Color(1, 1, 1, hovered ? 0.35f : 0.18f), false);

            // Bottom dark bevel
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), new Color(0, 0, 0, 0.35f), false);

            // Label
            var style = new GUIStyle(centered)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow
            };
            style.normal.textColor = (drawCol.grayscale > 0.55f) ? Ink : Cream;

            GUI.Label(r, label, style);

            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.clear;
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            GUI.backgroundColor = oldBg;
            return clicked;
        }

        public static bool CharacterCard(Rect r, string charName, string role, Color themeColor, bool isSelected)
        {
            bool hovered = r.Contains(MousePosition);

            // Card background
            Color bg = isSelected
                ? new Color(themeColor.r * 0.25f, themeColor.g * 0.25f, themeColor.b * 0.25f, 0.95f)
                : (hovered ? new Color(0.08f, 0.12f, 0.18f, 0.92f) : new Color(0.04f, 0.06f, 0.10f, 0.88f));

            Fill(new Rect(r.x + 2, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.45f), false);
            Fill(r, bg, false);

            // Border
            Color borderCol = isSelected
                ? themeColor
                : (hovered ? Color.Lerp(themeColor, Color.white, 0.35f) : new Color(1, 1, 1, 0.15f));
            int bSize = isSelected ? 2 : 1;
            Fill(new Rect(r.x, r.y, r.width, bSize), borderCol, false);
            Fill(new Rect(r.x, r.yMax - bSize, r.width, bSize), borderCol, false);
            Fill(new Rect(r.x, r.y, bSize, r.height), borderCol, false);
            Fill(new Rect(r.xMax - bSize, r.y, bSize, r.height), borderCol, false);

            if (isSelected)
            {
                Fill(new Rect(r.x, r.y, r.width, 3), themeColor, false);
            }

            // Tag badge (right)
            string tag = isSelected ? "★ ACTIVE" : "SELECT";
            var tagStyle = new GUIStyle(skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Overflow
            };
            tagStyle.normal.textColor = isSelected ? themeColor : Muted;
            GUI.Label(new Rect(r.xMax - 86, r.y + 8, 74, 22), tag, tagStyle);

            // Name (left) - strictly bounded so it never collides with tag badge
            var nameStyle = new GUIStyle(skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow
            };
            nameStyle.normal.textColor = isSelected ? Color.white : Cream;
            GUI.Label(new Rect(r.x + 12, r.y + 8, r.width - 100, 22), charName, nameStyle);

            // Role / Perk (bottom)
            var roleStyle = new GUIStyle(skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Overflow
            };
            roleStyle.normal.textColor = isSelected ? Color.Lerp(themeColor, Color.white, 0.65f) : Muted;
            GUI.Label(new Rect(r.x + 12, r.y + 32, r.width - 24, 20), role, roleStyle);

            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.clear;
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            GUI.backgroundColor = oldBg;
            return clicked;
        }

        // ════════════════════════════════════════════════════════
        //  MAIN MENU BUTTON — matches reference image exactly
        //  Dark translucent bg, coloured icon circle on left,
        //  ALL text is bold red-orange (same colour for all 3 buttons)
        // ════════════════════════════════════════════════════════
        public static bool MenuButton(Rect rect, string iconChar, string label, Color iconColor)
        {
            bool hovered = rect.Contains(MousePosition);

            // ── Drop shadow ──
            Fill(new Rect(rect.x + 5, rect.y + 6, rect.width, rect.height),
                 new Color(0, 0, 0, 0.55f), false);

            // ── Dark translucent background ──
            Color bg = hovered
                ? new Color(0.08f, 0.10f, 0.16f, 0.88f)
                : new Color(0.04f, 0.05f, 0.10f, 0.82f);
            Fill(rect, bg, false);

            // ── Border lines (subtle) ──
            float borderA = hovered ? 0.18f : 0.08f;
            Fill(new Rect(rect.x, rect.y, rect.width, 2), new Color(1, 1, 1, borderA), false);
            Fill(new Rect(rect.x, rect.y + rect.height - 2, rect.width, 2), new Color(1, 1, 1, borderA * 0.5f), false);
            Fill(new Rect(rect.x, rect.y, 2, rect.height), new Color(1, 1, 1, borderA * 0.5f), false);
            Fill(new Rect(rect.x + rect.width - 2, rect.y, 2, rect.height), new Color(1, 1, 1, borderA * 0.5f), false);

            // ── Icon circle (left side) ──
            float iconDiam = Mathf.Min(rect.height * 0.58f, 52);
            float iconX = rect.x + 16;
            float iconY = rect.y + (rect.height - iconDiam) * 0.5f;

            // Icon glow ring
            Fill(new Rect(iconX - 3, iconY - 3, iconDiam + 6, iconDiam + 6),
                 new Color(iconColor.r, iconColor.g, iconColor.b, 0.30f), false);
            // Icon solid fill
            Fill(new Rect(iconX, iconY, iconDiam, iconDiam), iconColor, false);

            // Icon character
            var iconStyle = new GUIStyle(centered)
            {
                fontSize = (int)(iconDiam * 0.50f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            iconStyle.normal.textColor = Cream;
            GUI.Label(new Rect(iconX, iconY, iconDiam, iconDiam), iconChar, iconStyle);

            // ── Bold red-orange text (SAME colour for ALL buttons) ──
            float textX = iconX + iconDiam + 16;
            Color txtCol = hovered
                ? Color.Lerp(BtnTextColor, Color.white, 0.25f)
                : BtnTextColor;

            var textStyle = new GUIStyle(skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            textStyle.normal.textColor = txtCol;

            GUI.Label(new Rect(textX, rect.y, rect.width - (textX - rect.x) - 12, rect.height),
                      label, textStyle);

            // ── Click target ──
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.clear;
            bool clicked = GUI.Button(rect, "", GUIStyle.none);
            GUI.backgroundColor = oldBg;

            return clicked;
        }

        // ════════════════════════════════════════════════════════
        //  TITLE BUTTON — crisp textured button with glowing hover
        // ════════════════════════════════════════════════════════
        public static bool DrawTitleButton(Rect rect, Texture2D btnTex, Color glowColor)
        {
            bool hovered = rect.Contains(MousePosition);
            if (hovered && btnTex != null)
            {
                // Smooth pulse / glow when hovered
                float pulse = 1.05f + Mathf.Sin(Time.realtimeSinceStartup * 8f) * 0.02f;
                float gw = rect.width * pulse;
                float gh = rect.height * pulse;
                Rect glowRect = new Rect(rect.x - (gw - rect.width) * 0.5f,
                                         rect.y - (gh - rect.height) * 0.5f,
                                         gw, gh);

                // Soft colored aura behind button
                var oldC = GUI.color;
                GUI.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.55f);
                GUI.DrawTexture(new Rect(glowRect.x - 8, glowRect.y - 8, glowRect.width + 16, glowRect.height + 16),
                                btnTex, ScaleMode.StretchToFill);

                // Scaled crisp button
                GUI.color = Color.white;
                GUI.DrawTexture(glowRect, btnTex, ScaleMode.StretchToFill);
                GUI.color = oldC;
            }

            // Clickable area
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.clear;
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.backgroundColor = oldBg;
            return clicked;
        }

        public static GUIStyle ModernInput
        {
            get
            {
                if (modernInputStyle == null)
                {
                    modernInputStyle = new GUIStyle(skin.textField)
                    {
                        fontSize = 16,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(10, 10, 2, 2)
                    };
                    modernInputStyle.normal.textColor = Cream;
                    modernInputStyle.focused.textColor = Color.white;
                    modernInputStyle.normal.background = flat;
                    modernInputStyle.focused.background = flat;
                }
                return modernInputStyle;
            }
        }



        // ──── Standard layout button ────
        public static bool Button(string text, Color color, params GUILayoutOption[] options)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = color;
            bool clicked = GUILayout.Button(text, options);
            GUI.backgroundColor = old;
            return clicked;
        }

        // ──── Rect button ────
        public static bool Button(Rect rect, string text, Color color)
        {
            bool isHovered = rect.Contains(MousePosition);
            Color drawColor = isHovered ? Color.Lerp(color, Color.white, 0.22f) : color;
            Fill(new Rect(rect.x + 2, rect.y + 3, rect.width, rect.height), Shadow, false);
            var old = GUI.backgroundColor;
            GUI.backgroundColor = drawColor;
            bool clicked = GUI.Button(rect, text);
            GUI.backgroundColor = old;
            Fill(new Rect(rect.x, rect.y, rect.width, 2), new Color(1f, 1f, 1f, 0.25f), false);
            return clicked;
        }

        // ════════════════════════════════════════════════════════
        //  LOGO — large, centred, optional glow, NO dark box
        // ════════════════════════════════════════════════════════
        public static void DrawLogo(Rect r, bool withGlow = false)
        {
            var tex = LogoTexture;
            if (tex != null)
            {
                var old = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
                GUI.color = old;
            }
            else
            {
                var ts = new GUIStyle(heading)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Overflow
                };
                ts.normal.textColor = HotOrange;
                GUI.Label(r, "WHO'S WATCHING?", ts);
            }
        }

        // ──── Section header with accent bar ────
        public static void SectionHeader(string text, Color accent, float width)
        {
            GUILayout.Space(4);
            GUILayout.Label(text, Heading);
            Rect lr = GUILayoutUtility.GetLastRect();
            Fill(new Rect(lr.x, lr.yMax + 2, width, 2), accent, false);
            GUILayout.Space(6);
        }

        // ──── Slider ────
        public static void Slider(Rect r, string label, float value, float min, float max, out float result)
        {
            float labelW = 160, valW = 55;
            float sliderW = r.width - labelW - valW - 20;
            GUI.Label(new Rect(r.x, r.y, labelW, r.height), label, BoldLabel);
            float trackH = 8, trackY = r.y + (r.height - trackH) * 0.5f;
            Fill(new Rect(r.x + labelW, trackY, sliderW, trackH), new Color(0.05f, 0.07f, 0.11f), false);
            float frac = Mathf.Clamp01((value - min) / Mathf.Max(0.001f, max - min));
            if (frac > 0) Fill(new Rect(r.x + labelW, trackY, sliderW * frac, trackH), Mint, false);
            result = GUI.HorizontalSlider(new Rect(r.x + labelW, r.y, sliderW, r.height), value, min, max);
            int pct = Mathf.RoundToInt(Mathf.Clamp01((result - min) / Mathf.Max(0.001f, max - min)) * 100);
            GUI.Label(new Rect(r.x + labelW + sliderW + 12, r.y, valW, r.height), $"{pct}%", BoldLabel);
        }

        // ──── Toggle ────
        public static bool Toggle(Rect r, string label, bool value)
        {
            float labelW = r.width - 85;
            GUI.Label(new Rect(r.x, r.y, labelW, r.height), label, BoldLabel);
            Rect btn = new Rect(r.x + labelW, r.y + (r.height - 30) * 0.5f, 76, 30);
            if (Button(btn, value ? "ON" : "OFF", value ? Mint : Muted)) return !value;
            return value;
        }

        // ──── CycleButton ────
        public static void CycleButton(Rect r, string label, string[] options, int currentIndex, out int newIndex)
        {
            newIndex = currentIndex;
            if (options == null || options.Length == 0) return;
            float labelW = 160, arrowW = 36;
            float textW = r.width - labelW - arrowW * 2 - 16;
            GUI.Label(new Rect(r.x, r.y, labelW, r.height), label, BoldLabel);
            float btnY = r.y + (r.height - 34) * 0.5f;
            if (Button(new Rect(r.x + labelW, btnY, arrowW, 34), "<", Cream))
                newIndex = (currentIndex - 1 + options.Length) % options.Length;
            string optText = options[Mathf.Clamp(currentIndex, 0, options.Length - 1)];
            Card(new Rect(r.x + labelW + arrowW + 4, btnY, textW, 34), PanelLight);
            GUI.Label(new Rect(r.x + labelW + arrowW + 4, btnY + 6, textW, 24), optText, Center);
            if (Button(new Rect(r.x + labelW + arrowW + textW + 8, btnY, arrowW, 34), ">", Cream))
                newIndex = (currentIndex + 1) % options.Length;
        }

        // ──── Progress Bar ────
        public static void ProgressBar(Rect r, float progress, Color fillColor, string text = "")
        {
            Fill(new Rect(r.x + 2, r.y + 2, r.width, r.height), Shadow, false);
            Fill(r, new Color(0.04f, 0.06f, 0.10f), false);
            float fillW = r.width * Mathf.Clamp01(progress);
            if (fillW > 0)
            {
                Fill(new Rect(r.x, r.y, fillW, r.height), fillColor, false);
                Fill(new Rect(r.x, r.y, fillW, 2), new Color(1, 1, 1, 0.25f), false);
            }
            if (!string.IsNullOrEmpty(text)) GUI.Label(r, text, Center);
        }

        public static void HudChoice()
        {
            if (Button(Details ? "HUD DETAILS: ON" : "HUD DETAILS: OFF", Cream)) Details = !Details;
        }

        public static void Reticle(bool interactive = false)
        {
            float cx = Width * 0.5f, cy = Height * 0.5f;
            if (interactive)
            {
                float pulse = 10f + Mathf.Sin(Time.time * 8f) * 2f;
                Fill(new Rect(cx - pulse, cy - pulse, 6, 2), Gold, false);
                Fill(new Rect(cx - pulse, cy - pulse, 2, 6), Gold, false);
                Fill(new Rect(cx + pulse - 6, cy - pulse, 6, 2), Gold, false);
                Fill(new Rect(cx + pulse - 2, cy - pulse, 2, 6), Gold, false);
                Fill(new Rect(cx - pulse, cy + pulse - 2, 6, 2), Gold, false);
                Fill(new Rect(cx - pulse, cy + pulse - 6, 2, 6), Gold, false);
                Fill(new Rect(cx + pulse - 6, cy + pulse - 2, 6, 2), Gold, false);
                Fill(new Rect(cx + pulse - 2, cy + pulse - 6, 2, 6), Gold, false);
                Fill(new Rect(cx - 2, cy - 2, 4, 4), Mint, false);
            }
            else Fill(new Rect(cx - 3, cy - 3, 6, 6), Cream, false);
        }

        public static Vector2 MousePosition
        {
            get
            {
                if (Mouse.current == null) return Vector2.zero;
                var p = Mouse.current.position.ReadValue() / Mathf.Max(0.1f, Scale);
                p.y = Height - p.y;
                return p;
            }
        }

        public static bool PointerOverMenu()
        {
            if (Cursor.lockState == CursorLockMode.Locked || Mouse.current == null) return false;
            var p = MousePosition;
            return new Rect(Width - 360, 20, 340, 500).Contains(p)
                || new Rect(Width / 2 - 350, Height / 2 - 300, 700, 600).Contains(p);
        }
    }
}
