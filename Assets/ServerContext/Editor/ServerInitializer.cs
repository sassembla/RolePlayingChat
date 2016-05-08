using XrossPeerUtility;

using UnityEngine;
using UnityEditor;

using System;
using System.IO;


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
		
		var disqueConnectionCont = new DisqueConnectionController(SetupUpdater, settings.ClientToContextKey(), settings.DataMode());
		disqueConnectionCont.SetContext(sContext);
				
		// EditorApplication.update += DetectCompileStart;
		// EditorApplication.playmodeStateChanged += DetectPlayStart;
	}
	
	
	private static void DetectCompileStart () {
		if (EditorApplication.isCompiling) {
			EditorApplication.update -= DetectCompileStart;
			DisquuunTests.StopTests();
			if (sContext != null) sContext.Teardown();
		}
	}
	
	private static void DetectPlayStart () {
		if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode) {
			if (sContext != null) sContext.Teardown();
		}
	}
	
	public static void SetupUpdater (string loopId, Func<bool> OnUpdate) {
		
		// #if UNITY_EDITOR
		// {
		// 	var settings = (StandardAssetsConnectorSettings)ScriptableObject.CreateInstance("StandardAssetsConnectorSettings");
		// 	if (settings.use_unity_thread) {
		// 		Action update = () => OnUpdate();
		// 		var executor = new UnityEditorUpdateExecutor(update);
		// 		EditorApplication.update += executor.Update;
		// 		return;
		// 	}
		// }
		// #endif
		
		// if not unity
		new Updater("serverInitializer_" + loopId, OnUpdate);
	}


	// あとはこのへんにコマンドを追加するかね。停止と再起動、リセット。contextのリセットとかを呼べば良い感じ。
}
