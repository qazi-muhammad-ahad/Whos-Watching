using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WhosWatching.Editor
{
    public static class AppearancePreview
    {
        const string Root="Assets/_WhosWatching/";
        static float motionTime = -1;
        public static void RenderMotion()
        {
            for(int i=0;i<4;i++)
            {
                motionTime=i*0.16f; Render();
                File.Copy("Logs/AppearancePreview.png",$"Logs/MotionPreview-{i}.png",true);
            }
            motionTime=-1;
        }
        public static void RenderJump()
        {
            jumpPreview=true; Render(); jumpPreview=false;
            File.Copy("Logs/AppearancePreview.png","Logs/JumpPreview.png",true);
        }
        public static void RenderJumpAndBuild()
        {
            RenderJump(); EditorSceneManager.OpenScene(Root+"Scenes/Main.unity"); ProjectSetup.Build();
        }
        static bool jumpPreview;
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(0.65f,0.65f,0.65f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position=new Vector3(0,-0.2f,0); floor.transform.localScale=new Vector3(20,0.3f,20);
            floor.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(0.16f,0.2f,0.26f)};
            var sun=new GameObject("Light").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=2; sun.transform.rotation=Quaternion.Euler(45,-35,0);
            Make("Prefabs/Character/Devil.prefab",new Vector3(-2.1f,0,0),TeamId.A,"Devil_Move");
            Make("Prefabs/Character/Ghost.prefab",new Vector3(-0.7f,0,0),TeamId.A,"Ghost_Move");
            Make("Prefabs/Character/Devil.prefab",new Vector3(0.7f,0,0),TeamId.B,"Devil_Move");
            Make("Prefabs/Character/Ghost.prefab",new Vector3(2.1f,0,0),TeamId.B,"Ghost_Move");
            Make("Prefabs/TeamReady/Crate_Medium.prefab",new Vector3(-3.3f,0,-1.6f),TeamId.A);
            Make("Prefabs/TeamReady/Crate_Medium.prefab",new Vector3(3.3f,0,-1.6f),TeamId.B);
            Make("Prefabs/TeamReady/Banner_Blue_Eye.prefab",new Vector3(-2.4f,0,-2.7f),TeamId.A);
            Make("Prefabs/TeamReady/Banner_Red_Eye.prefab",new Vector3(2.4f,0,-2.7f),TeamId.B);
            var camera=new GameObject("Preview camera").AddComponent<Camera>(); camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camera.transform.position=new Vector3(0,4.6f,10); camera.transform.LookAt(new Vector3(0,1,-0.8f));
            camera.backgroundColor=new Color(0.07f,0.09f,0.13f); camera.clearFlags=CameraClearFlags.SolidColor; camera.fieldOfView=43;
            var target=new RenderTexture(1400,900,24); camera.targetTexture=target;
            camera.Render(); RenderTexture.active=target;
            var image=new Texture2D(1400,900,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1400,900),0,0); image.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/AppearancePreview.png",image.EncodeToPNG());
            RenderTexture.active=null; camera.targetTexture=null;
            Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            Debug.Log("WW_APPEARANCE_PREVIEW_RENDERED");
        }
        static void Make(string path,Vector3 position,TeamId team,string clip=null)
        {
            var obj=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+path));
            obj.transform.position=position;
            if(clip!=null)
            {
                if(jumpPreview) clip=clip.Replace("_Move","_Jump");
                AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"Animations/"+clip+".anim").SampleAnimation(obj,motionTime<0 ? 0.23f : motionTime);
                if(motionTime>=0) obj.GetComponent<CharacterPresentation>().SampleMotion(motionTime*Mathf.PI*2*1.8f,5);
                if(jumpPreview)
                {
                    obj.transform.position+=Vector3.up*0.6f;
                    var presentation=obj.GetComponent<CharacterPresentation>(); presentation.SampleMotion(0,0,0); presentation.ApplyJumpShape(1,0);
                }
            }
            foreach(var tint in obj.GetComponentsInChildren<TeamTint>()) { tint.Team=team; tint.Apply(); }
        }
    }
}
