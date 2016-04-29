using XrossPeerUtility;

using UnityEngine;
using UnityEditor;

using System;
using System.IO;


/*
	このクラス自体がUnityに依存しているので、なんかうまい抽象化を考えないとな
*/
[InitializeOnLoad] public class ServerInitializer {
	[MenuItem ("ServerInitializer/Regenerate Private Client Key", false, 1)] public static void RegenerateClientRandomKey () {
  		var settings = (StandardAssetsConnectorSettings)ScriptableObject.CreateInstance("StandardAssetsConnectorSettings");
		settings.GeneratePrivateClientKey();
  	}
  
	static ServerContext sContext;
	
	static ServerInitializer () {
		XrossPeer.SetupLog(Path.Combine(Directory.GetParent(Application.dataPath).ToString(), "server.log"));
		Setup();
	}
	
	public static void Setup () {
		XrossPeer.Log("\n\n");
		XrossPeer.Log("----------");
		XrossPeer.Log("initializing server context....");
		XrossPeer.Log("----------");
		
		var settings = (StandardAssetsConnectorSettings)ScriptableObject.CreateInstance("StandardAssetsConnectorSettings");
		
		
		sContext = new ServerContext(settings.ClientToContextKey());
		
		EditorApplication.update += DetectCompileStart;
		EditorApplication.playmodeStateChanged += DetectPlayStart;
	}
	
	
	private static void DetectCompileStart () {
		if (EditorApplication.isCompiling) {
			EditorApplication.update -= DetectCompileStart;
			if (sContext != null) sContext.Teardown();
		}
	}
	
	private static void DetectPlayStart () {
		if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode) {
			if (sContext != null) sContext.Teardown();
		}
	}


	// あとはこのへんにコマンドを追加するかね。停止と再起動、リセット。contextのリセットとかを呼べば良い感じ。
}
