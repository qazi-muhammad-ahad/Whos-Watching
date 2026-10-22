using System;
using UnityEngine;
using UnityEngine.InputSystem;
namespace WhosWatching
{
    // Shared IMGUI theme: crisp, resolution-independent panels for the low-poly arena.
    public static class GameUI
    {
        public static readonly Color Ink = new(0.035f,0.055f,0.085f), Cream = new(0.98f,0.97f,0.91f), Gold = new(1f,0.62f,0.20f), Mint = new(0.20f,0.88f,0.76f);
        public static readonly Color Panel = new(0.035f,0.065f,0.09f,0.97f), Muted = new(0.70f,0.77f,0.79f), Line = new(0.28f,0.40f,0.43f,0.72f);
        static GUISkin skin;
        static Texture2D flat;
        static GUIStyle title, display, heading, small, centered, overline, step;
        static bool? details;
        public static bool Details { get => details ??= PlayerPrefs.GetInt("WW.DetailedHUD",1)!=0; set { details=value; PlayerPrefs.SetInt("WW.DetailedHUD",value?1:0); PlayerPrefs.Save(); } }
        public static float Scale => Mathf.Min(Screen.width/1280f,Screen.height/720f);
        public static float Width => Screen.width/Mathf.Max(0.1f,Scale);
        public static float Height => Screen.height/Mathf.Max(0.1f,Scale);
        public static GUIStyle Title => title;
        public static GUIStyle Display => display;
        public static GUIStyle Heading => heading;
        public static GUIStyle Small => small;
        public static GUIStyle Center => centered;
        public static GUIStyle Overline => overline;
        public static GUIStyle Step => step;
        public static Scope Begin() { Ensure(); return new Scope(false); }
        public struct Scope : IDisposable
        {
            readonly GUISkin oldSkin; readonly Matrix4x4 matrix; readonly Color color,background,content;
            readonly bool enabled; readonly int depth;
            public Scope(bool unused = false) { oldSkin=GUI.skin; matrix=GUI.matrix; color=GUI.color; background=GUI.backgroundColor; content=GUI.contentColor;enabled=GUI.enabled;depth=GUI.depth;
                GUI.skin=skin;GUI.matrix=Matrix4x4.Scale(Vector3.one*Mathf.Max(0.1f,Scale));GUI.color=GUI.backgroundColor=GUI.contentColor=Color.white; }
            public void Dispose() { GUI.skin=oldSkin; GUI.matrix=matrix;GUI.color=color;GUI.backgroundColor=background;GUI.contentColor=content;GUI.enabled=enabled;GUI.depth=depth; }
        }
        static void Ensure()
        {
            if(skin) return;
            flat=Texture2D.whiteTexture;
            skin=UnityEngine.Object.Instantiate(GUI.skin); skin.hideFlags=HideFlags.HideAndDontSave;
            skin.label.fontSize=17;skin.label.normal.textColor=Cream;skin.label.wordWrap=true;skin.label.padding=new RectOffset(0,0,4,4);
            skin.button=new GUIStyle(skin.button) {fontSize=17,fontStyle=FontStyle.Bold,fixedHeight=46,margin=new RectOffset(4,4,6,6),border=new RectOffset(10,10,10,10),alignment=TextAnchor.MiddleCenter};
            skin.button.normal.background=flat;skin.button.normal.textColor=Ink;
            skin.button.hover.background=flat;skin.button.hover.textColor=Ink;
            skin.button.active.background=flat;skin.button.active.textColor=Ink;
            skin.button.onNormal.background=flat;skin.button.onNormal.textColor=Ink;
            skin.button.onHover.background=flat;skin.button.onHover.textColor=Ink;
            skin.button.onActive.background=flat;skin.button.onActive.textColor=Ink;
            skin.textField=new GUIStyle(skin.textField) {fontSize=19,fixedHeight=42,padding=new RectOffset(14,14,9,7),border=new RectOffset(10,10,10,10)};
            skin.textField.normal.background=flat;skin.textField.normal.textColor=Ink;skin.textField.focused.background=flat;skin.textField.focused.textColor=Ink;
            title=new GUIStyle(skin.label){fontSize=54,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft};
            display=new GUIStyle(skin.label){fontSize=40,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            heading=new GUIStyle(skin.label){fontSize=21,fontStyle=FontStyle.Bold};
            small=new GUIStyle(skin.label){fontSize=14};small.normal.textColor=Muted;
            centered=new GUIStyle(skin.label){alignment=TextAnchor.MiddleCenter,fontSize=15,fontStyle=FontStyle.Bold};
            overline=new GUIStyle(skin.label){fontSize=13,fontStyle=FontStyle.Bold};overline.normal.textColor=Gold;
            step=new GUIStyle(skin.label){fontSize=22,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};step.normal.textColor=Mint;
        }
        public static void Fill(Rect r,Color c,bool rounded=true)
        {
            var old=GUI.color;
            GUI.color=c;GUI.DrawTexture(r,flat);
            GUI.color=old;
        }
        public static void Card(Rect r,Color? color=null) { Fill(r,color??Panel); }
        public static bool Button(string text,Color color,params GUILayoutOption[] options) { var old=GUI.backgroundColor;GUI.backgroundColor=color;bool clicked=GUILayout.Button(text,options);GUI.backgroundColor=old;return clicked; }
        public static bool Button(Rect rect,string text,Color color) { var old=GUI.backgroundColor;GUI.backgroundColor=color;bool clicked=GUI.Button(rect,text);GUI.backgroundColor=old;return clicked; }
        public static void HudChoice() { if(Button(Details?"HUD DETAILS: ON":"HUD DETAILS: OFF",Cream)) Details=!Details; }
        public static void Reticle() { Fill(new Rect(Width/2-3,Height/2-3,6,6),Cream); }
        public static bool PointerOverMenu()
        {
            if(Cursor.lockState==CursorLockMode.Locked || Mouse.current==null) return false;
            var p=Mouse.current.position.ReadValue()/Mathf.Max(0.1f,Scale);p.y=Height-p.y;
            return new Rect(Width-300,20,280,310).Contains(p);
        }
    }
}



