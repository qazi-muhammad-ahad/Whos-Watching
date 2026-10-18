using System;
using UnityEngine;
using UnityEngine.InputSystem;
namespace WhosWatching
{
    // Shared IMGUI theme: resolution-independent cards with soft corners and strong outlines.
    public static class GameUI
    {
        public static readonly Color Ink = new(0.075f,0.09f,0.18f), Cream = new(1,0.96f,0.84f), Gold = new(1,0.77f,0.23f), Mint = new(0.35f,0.91f,0.72f);
        static GUISkin skin;
        static Texture2D round, flat;
        static GUIStyle title, heading, small, centered;
        static bool? details;
        public static bool Details { get => details ??= PlayerPrefs.GetInt("WW.DetailedHUD",1)!=0; set { details=value; PlayerPrefs.SetInt("WW.DetailedHUD",value?1:0); PlayerPrefs.Save(); } }
        public static float Scale => Mathf.Min(Screen.width/1280f,Screen.height/720f);
        public static float Width => Screen.width/Mathf.Max(0.1f,Scale);
        public static float Height => Screen.height/Mathf.Max(0.1f,Scale);
        public static GUIStyle Title => title;
        public static GUIStyle Heading => heading;
        public static GUIStyle Small => small;
        public static GUIStyle Center => centered;
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
            round=new Texture2D(32,32,TextureFormat.RGBA32,false);round.hideFlags=HideFlags.HideAndDontSave;
            for(int y=0;y<32;y++) for(int x=0;x<32;x++) {
                float dx=Mathf.Max(8-x,0,x-23),dy=Mathf.Max(8-y,0,y-23);
                round.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(8.5f-Mathf.Sqrt(dx*dx+dy*dy))));
            }
            round.Apply(); flat=Texture2D.whiteTexture;
            skin=UnityEngine.Object.Instantiate(GUI.skin); skin.hideFlags=HideFlags.HideAndDontSave;
            skin.label.fontSize=16;skin.label.normal.textColor=Cream;skin.label.wordWrap=true;skin.label.padding=new RectOffset(0,0,3,3);
            skin.button=new GUIStyle(skin.button) {fontSize=17,fontStyle=FontStyle.Bold,fixedHeight=40,margin=new RectOffset(4,4,5,5),border=new RectOffset(10,10,10,10)};
            skin.button.normal.background=round;skin.button.normal.textColor=Ink;
            skin.button.hover.background=round;skin.button.hover.textColor=Ink;
            skin.button.active.background=round;skin.button.active.textColor=Ink;
            skin.button.onNormal.background=round;skin.button.onNormal.textColor=Ink;
            skin.button.onHover.background=round;skin.button.onHover.textColor=Ink;
            skin.button.onActive.background=round;skin.button.onActive.textColor=Ink;
            skin.textField=new GUIStyle(skin.textField) {fontSize=18,fixedHeight=38,padding=new RectOffset(12,12,8,6),border=new RectOffset(10,10,10,10)};
            skin.textField.normal.background=round;skin.textField.normal.textColor=Ink;skin.textField.focused.background=round;skin.textField.focused.textColor=Ink;
            title=new GUIStyle(skin.label){fontSize=44,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            heading=new GUIStyle(skin.label){fontSize=22,fontStyle=FontStyle.Bold};
            small=new GUIStyle(skin.label){fontSize=13};small.normal.textColor=new Color(0.72f,0.79f,0.88f);
            centered=new GUIStyle(skin.label){alignment=TextAnchor.MiddleCenter,fontSize=17,fontStyle=FontStyle.Bold};
        }
        public static void Fill(Rect r,Color c,bool rounded=true) { var old=GUI.color;GUI.color=c; if(rounded) GUI.DrawTexture(r,round,ScaleMode.StretchToFill,true,0,Color.white,0,9);else GUI.DrawTexture(r,flat);GUI.color=old; }
        public static void Card(Rect r,Color? color=null) { Fill(new Rect(r.x,r.y+5,r.width,r.height),new Color(0.025f,0.035f,0.08f,0.85f));Fill(r,color??new Color(0.12f,0.15f,0.25f,0.97f)); }
        public static bool Button(string text,Color color,params GUILayoutOption[] options) { var old=GUI.backgroundColor;GUI.backgroundColor=color;bool clicked=GUILayout.Button(text,options);GUI.backgroundColor=old;return clicked; }
        public static void HudChoice() { if(Button(Details?"HUD: DETAILED  /  show essentials":"HUD: ESSENTIALS  /  show details",Cream)) Details=!Details; }
        public static void Reticle() { Fill(new Rect(Width/2-3,Height/2-3,6,6),Cream); }
        public static bool PointerOverMenu()
        {
            if(Cursor.lockState==CursorLockMode.Locked || Mouse.current==null) return false;
            var p=Mouse.current.position.ReadValue()/Mathf.Max(0.1f,Scale);p.y=Height-p.y;
            return new Rect(Width-300,20,280,255).Contains(p);
        }
    }
}



