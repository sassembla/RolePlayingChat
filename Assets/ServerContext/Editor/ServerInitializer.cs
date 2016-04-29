using XrossPeerUtility;

using UnityEngine;
using UnityEditor;

using System;
using System.IO;


/*
	このクラス自体がUnityに依存しているので、なんかうまい抽象化を考えないとな
*/
[InitializeOnLoad] public class ServerInitializer {
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
	}
	
	
	private static void DetectCompileStart () {
		if (EditorApplication.isCompiling) {
			EditorApplication.update -= DetectCompileStart;
			sContext.Teardown();
		}
	}


	// あとはこのへんにコマンドを追加するかね。停止と再起動、リセット。contextのリセットとかを呼べば良い感じ。
}
