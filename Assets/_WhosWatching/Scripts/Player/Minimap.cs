using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WhosWatching
{
    // "Crew radar": a circular, north-up neon minimap for the local player.
    // Only the local player and teammates are plotted; opponents are never drawn. The arena
    // backdrop is a one-off top-down snapshot taken with every player's renderers hidden, so
    // rival bodies cannot leak into the image either.
    public static class Minimap
    {
        public const float Diameter = 220, Margin = 30;
        const int BakeSize = 512;
        static readonly Color Key = new(1, 0, 1, 1); // clear colour, keyed out after the bake
        static Texture2D disc, thinRing, glow, sweep, rainbow, arrow, arena;
        static ArenaGameplay bakedFor;
        static Vector2 worldCenter;
        static float worldRadius = 25;
        static GUIStyle tag;

        // Who appears on the local player's map.
        public static bool Shows(TeamId viewerTeam, bool isViewer, TeamId otherTeam) =>
            isViewer || (viewerTeam != TeamId.None && otherTeam == viewerTeam);

        // Called from the owner's LateUpdate: snapshot each arena once, outside of OnGUI.
        public static void Tick()
        {
            var a = ArenaGameplay.Instance;
            if (!a || !a.IsSpawned || a == bakedFor || NetworkGameManager.Instance == null) return;
            bakedFor = a;
            ComputeBounds(a);
            if (arena) Object.Destroy(arena);
            arena = null;
            try { arena = Bake(); }
            catch (Exception e) { Debug.LogWarning($"WW_MINIMAP arena snapshot failed, using plain radar: {e.Message}"); }
        }

        public static void Draw(NetworkPlayer viewer)
        {
            if (!viewer || NetworkGameManager.Instance == null || Event.current.type != EventType.Repaint) return;
            EnsureTextures();
            if (!ArenaGameplay.Instance) { worldCenter = Vector2.zero; worldRadius = 25; }
            tag ??= new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = new RectOffset(0, 0, 0, 0) };

            float t = Time.unscaledTime, r = Diameter / 2, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
            var lobby = TeamLobby.Instance;
            Color team = lobby ? lobby.Color(viewer.Team.Value) : GameUI.Mint;
            var c = new Vector2(Margin + r + 10, Margin + r + 10);

            // Backdrop: team-coloured bloom, dark rim, violet base, arena snapshot.
            Tex(Around(c, Diameter + 100), glow, Alpha(team, 0.30f + 0.15f * pulse));
            Tex(Around(c, Diameter + 10), disc, new Color(0.02f, 0.02f, 0.05f, 0.95f));
            Tex(Around(c, Diameter), disc, new Color(0.09f, 0.05f, 0.18f, 1f));
            Tex(Around(c, Diameter * 0.95f), glow, new Color(0.35f, 0.12f, 0.60f, 0.55f));
            if (arena) Tex(Around(c, Diameter), arena, Color.white);

            // Radar grid and sweep.
            Tex(Around(c, Diameter / 3), thinRing, new Color(1, 1, 1, 0.12f));
            Tex(Around(c, Diameter * 2 / 3), thinRing, new Color(1, 1, 1, 0.12f));
            GameUI.Fill(new Rect(c.x - r + 8, c.y - 0.5f, Diameter - 16, 1), new Color(1, 1, 1, 0.10f), false);
            GameUI.Fill(new Rect(c.x - 0.5f, c.y - r + 8, 1, Diameter - 16), new Color(1, 1, 1, 0.10f), false);
            Rotated(c, t * 100 % 360, () => Tex(Around(c, Diameter), sweep, Alpha(GameUI.Mint, 0.6f)));

            // Checkpoints: hollow diamonds until built, then solid and glowing.
            var a = ArenaGameplay.Instance;
            if (a)
                for (int i = 0; i < a.Checkpoints.Length; i++)
                {
                    if (!a.Checkpoints[i]) continue;
                    Color color = lobby ? lobby.Color(i < a.Teams.Length ? a.Teams[i] : TeamId.None) : TeamPalette.Neutral;
                    bool done = i < a.Completed.Count && a.Completed[i];
                    var p = ToMap(a.Checkpoints[i].position, c, r);
                    if (done) Tex(Around(p, 30), glow, Alpha(color, 0.5f + 0.4f * pulse));
                    Rotated(p, 45, () =>
                    {
                        GameUI.Fill(Around(p, 13), GameUI.Ink, false);
                        GameUI.Fill(Around(p, 10), color, false);
                        if (!done) GameUI.Fill(Around(p, 5), GameUI.Ink, false);
                    });
                }

            // Teammates (never opponents).
            TeamId viewerTeam = viewer.Team.Value;
            foreach (var mate in NetworkGameManager.Instance.Gaze.Players)
            {
                if (!mate || !mate.IsSpawned || mate == viewer || !Shows(viewerTeam, false, mate.Team.Value)) continue;
                var p = ToMap(mate.transform.position, c, r);
                Color color = lobby ? lobby.Color(mate.Team.Value) : GameUI.Cream;
                Tex(Around(p, 40), glow, Alpha(color, 0.45f + 0.35f * pulse));
                Tex(Around(p, 20), disc, GameUI.Ink);
                Tex(Around(p, 16), disc, color);
                float facing = mate.transform.eulerAngles.y;
                Rotated(p, facing, () => Tex(Around(p, 12), arrow, GameUI.Ink));
                Label(new Rect(p.x - 50, p.y + 10, 100, 16), Short(mate.DisplayName.Value.ToString()), color);
            }

            // Local player on top: expanding ping, glow and a bold heading arrow.
            var me = ToMap(viewer.transform.position, c, r);
            float phase = t * 0.8f % 1;
            Tex(Around(me, Mathf.Lerp(14, 64, phase)), thinRing, Alpha(GameUI.Cream, (1 - phase) * 0.9f));
            Tex(Around(me, 46), glow, Alpha(team, 0.85f));
            float yaw = viewer.View ? viewer.View.transform.eulerAngles.y : viewer.transform.eulerAngles.y;
            Rotated(me, yaw, () =>
            {
                Tex(Around(me, 30), arrow, GameUI.Ink);
                Tex(Around(me, 22), arrow, Color.white);
            });

            // Spinning rainbow frame and north badge.
            Rotated(c, t * 35 % 360, () => Tex(Around(c, Diameter + 18), rainbow, Color.white));
            Tex(Around(c, Diameter + 18), glow, new Color(1, 1, 1, 0.04f));
            var north = new Vector2(c.x, c.y - r - 5);
            Tex(Around(north, 28), disc, GameUI.Ink);
            Tex(Around(north, 28), thinRing, GameUI.Gold);
            Label(Around(north, 28), "N", GameUI.Gold);

            // Name pill.
            var pill = new Rect(c.x - 86, c.y + r + 16, 172, 28);
            GameUI.Fill(pill, new Color(0.02f, 0.02f, 0.05f, 0.92f), false);
            GameUI.Fill(new Rect(pill.x, pill.y, 5, pill.height), team, false);
            GameUI.Fill(new Rect(pill.xMax - 5, pill.y, 5, pill.height), GameUI.Mint, false);
            Tex(Around(new Vector2(pill.x + 20, pill.center.y), 10), disc, Color.Lerp(GameUI.Mint, Color.white, pulse));
            GUI.Label(new Rect(pill.x + 30, pill.y, pill.width - 36, pill.height), "CREW RADAR", GameUI.Center);
        }

        static string Short(string name) => name.Length > 10 ? name.Substring(0, 10) : name;

        static void Label(Rect rect, string text, Color color)
        {
            tag.normal.textColor = GameUI.Ink;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, tag);
            tag.normal.textColor = color;
            GUI.Label(rect, text, tag);
        }

        static Vector2 ToMap(Vector3 world, Vector2 center, float radius)
        {
            var offset = new Vector2(world.x - worldCenter.x, -(world.z - worldCenter.y)) / worldRadius * radius;
            return center + Vector2.ClampMagnitude(offset, radius - 10);
        }

        static Color Alpha(Color c, float a) { c.a = a; return c; }
        static Rect Around(Vector2 c, float size) => new(c.x - size / 2, c.y - size / 2, size, size);

        static void Tex(Rect rect, Texture texture, Color color)
        {
            var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, texture); GUI.color = old;
        }

        // Rotates in local GUI space (GUIUtility.RotateAroundPivot misplaces the pivot under a scaled GUI.matrix).
        static void Rotated(Vector2 pivot, float degrees, Action draw)
        {
            var old = GUI.matrix;
            GUI.matrix = old * Matrix4x4.TRS(pivot, Quaternion.Euler(0, 0, degrees), Vector3.one) * Matrix4x4.Translate(-pivot);
            draw();
            GUI.matrix = old;
        }

        static void ComputeBounds(ArenaGameplay a)
        {
            var points = new List<Vector3>();
            if (a.SpawnA) points.Add(a.SpawnA.position);
            if (a.SpawnB) points.Add(a.SpawnB.position);
            foreach (var checkpoint in a.Checkpoints) if (checkpoint) points.Add(checkpoint.position);
            points.AddRange(a.CrateHomes);
            if (points.Count == 0) { worldCenter = Vector2.zero; worldRadius = 25; return; }
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in points) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            worldCenter = new Vector2((minX + maxX) / 2, (minZ + maxZ) / 2);
            float radius = 0;
            foreach (var p in points) radius = Mathf.Max(radius, Vector2.Distance(worldCenter, new Vector2(p.x, p.z)));
            worldRadius = Mathf.Max(10, radius + 7);
        }

        // Synchronous top-down render with all player renderers hidden, then keyed, masked and colour-boosted.
        static Texture2D Bake()
        {
            var hidden = new List<Renderer>();
            foreach (var player in NetworkGameManager.Instance.Gaze.Players)
                if (player)
                    foreach (var renderer in player.GetComponentsInChildren<Renderer>())
                        if (renderer.enabled) { renderer.enabled = false; hidden.Add(renderer); }
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var rt = RenderTexture.GetTemporary(BakeSize, BakeSize, 24, RenderTextureFormat.ARGB32);
            var go = new GameObject("WW_MinimapBake") { hideFlags = HideFlags.HideAndDontSave };
            var previous = RenderTexture.active;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = worldRadius; cam.aspect = 1;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Key;
                cam.nearClipPlane = 0.3f; cam.farClipPlane = 600; cam.targetTexture = rt;
                go.transform.SetPositionAndRotation(new Vector3(worldCenter.x, 250, worldCenter.y), Quaternion.Euler(90, 0, 0));
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else cam.Render();
                RenderTexture.active = rt;
                var texture = new Texture2D(BakeSize, BakeSize, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                texture.ReadPixels(new Rect(0, 0, BakeSize, BakeSize), 0, 0);
                Stylize(texture);
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(go);
                RenderTexture.ReleaseTemporary(rt);
                RenderSettings.fog = fog;
                foreach (var renderer in hidden) if (renderer) renderer.enabled = true;
            }
        }

        static void Stylize(Texture2D texture)
        {
            var pixels = texture.GetPixels();
            int n = texture.width;
            float px = 2f / n;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    var c = pixels[i];
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1, d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool background = c.r > 0.85f && c.b > 0.85f && c.g < 0.2f;
                    if (d >= 1 || background) { pixels[i] = Color.clear; continue; }
                    Color.RGBToHSV(c, out float h, out float s, out float v);
                    s = Mathf.Clamp01(s * 1.45f + 0.05f);
                    v = Mathf.Clamp01(Mathf.Pow(v, 0.9f) * 1.1f) * Mathf.Lerp(1, 0.6f, d * d * d);
                    var styled = Color.HSVToRGB(h, s, v);
                    styled.a = 0.92f * Mathf.Clamp01((1 - d) / (px * 3));
                    pixels[i] = styled;
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
        }

        static void EnsureTextures()
        {
            if (disc) return;
            const int size = 128;
            const float px = 2f / size;
            disc = Make(size, (r, _, _, _) => White(Mathf.Clamp01((1 - r) / (px * 1.5f))));
            thinRing = Make(size, (r, _, _, _) => White(Mathf.Clamp01(Mathf.Min(1 - r, r - 0.94f) / px)));
            glow = Make(size, (r, _, _, _) => White(Mathf.Pow(Mathf.Clamp01(1 - r), 2.2f)));
            sweep = Make(size, (r, angle, _, _) =>
            {
                float trail = (360 - angle) / 85;
                return White(trail <= 1 ? Mathf.Pow(1 - trail, 2) * Mathf.Clamp01((1 - r) / px) : 0);
            });
            rainbow = Make(size, (r, angle, _, _) =>
            {
                var color = Color.HSVToRGB(angle / 360, 0.75f, 1);
                color.a = Mathf.Clamp01(Mathf.Min(1 - r, r - 0.91f) / px);
                return color;
            });
            var tip = new Vector2(0, 0.9f); var left = new Vector2(-0.68f, -0.78f);
            var notch = new Vector2(0, -0.38f); var right = new Vector2(0.68f, -0.78f);
            arrow = Make(size, (_, _, x, y) =>
            {
                var p = new Vector2(x, y);
                return White(Mathf.Clamp01(Mathf.Max(Inside(p, tip, left, notch), Inside(p, tip, notch, right)) / px));
            });
        }

        static Color White(float a) => new(1, 1, 1, a);

        // Signed distance inside a triangle (positive inside), orientation-independent.
        static float Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float sign = Mathf.Sign((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
            return Mathf.Min(Edge(p, a, b) * sign, Mathf.Min(Edge(p, b, c) * sign, Edge(p, c, a) * sign));
        }

        static float Edge(Vector2 p, Vector2 a, Vector2 b)
        {
            var e = b - a;
            return (e.x * (p.y - a.y) - e.y * (p.x - a.x)) / e.magnitude;
        }

        // shape(radius, clockwise angle from up in degrees, x, y) with x/y in [-1, 1] and y up.
        static Texture2D Make(int size, Func<float, float, float, float, Color> shape)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = (x + 0.5f) / size * 2 - 1, fy = (y + 0.5f) / size * 2 - 1;
                    float angle = Mathf.Repeat(Mathf.Atan2(fx, fy) * Mathf.Rad2Deg, 360);
                    pixels[y * size + x] = shape(Mathf.Sqrt(fx * fx + fy * fy), angle, fx, fy);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
