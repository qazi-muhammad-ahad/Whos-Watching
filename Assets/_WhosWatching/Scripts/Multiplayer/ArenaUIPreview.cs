#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace WhosWatching
{
    public sealed class ArenaUIPreview : MonoBehaviour
    {
        string folder;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-wwUI");
            if(i>=0 && i+1<args.Length) new GameObject("UI preview").AddComponent<ArenaUIPreview>().folder=args[i+1];
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);bool original=GameUI.Details;
            GameUI.Details=true;
            yield return new WaitForSeconds(2);yield return Capture("UI-Lobby.png");
            NetworkGameManager.Instance.StartSession(true);
            yield return new WaitForSeconds(2);yield return Capture("UI-Ready.png");
            TeamLobby.Instance.Playing.Value=true;ArenaGameplay.Instance.SpawnEveryone();
            Cursor.lockState=CursorLockMode.Locked;
            yield return new WaitForSeconds(2);yield return Capture("UI-HUD.png");
            GameUI.Details=false;yield return new WaitForSeconds(0.5f);yield return Capture("UI-Essentials.png");
            ArenaGameplay.Instance.Winner.Value=TeamId.A;ArenaGameplay.Instance.Finished.Value=true;Cursor.lockState=CursorLockMode.Locked;
            yield return new WaitForSeconds(0.5f);yield return Capture("UI-Results.png");
            GameUI.Details=original;Debug.Log("WW_UI_PREVIEW_PASS");Application.Quit();
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame(); var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0); image.Apply(); File.WriteAllBytes(Path.Combine(folder,name),image.EncodeToPNG()); Destroy(image);yield return new WaitForSeconds(0.5f);
        }
    }
}
#endif


