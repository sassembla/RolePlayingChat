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
		
		DisquuunTests.Start();
		EditorApplication.update += DetectCompileStart;
	}
	
	private static void DetectCompileStart () {
		if (EditorApplication.isCompiling) {
			EditorApplication.update -= DetectCompileStart;
			if (disqueConnectionCont != null) disqueConnectionCont.Disconnect();
			DisquuunTests.Stop();
		}
	}
	
	private static DisqueConnectionController disqueConnectionCont;
	
	
	public static void Setup () {
		XrossPeer.Log("\n\n");
		XrossPeer.Log("----------");
		XrossPeer.Log("initializing server context....");
		XrossPeer.Log("----------");
		
		var settings = (StandardAssetsConnectorSettings)ScriptableObject.CreateInstance("StandardAssetsConnectorSettings");
		
		sContext = new ServerContext(settings.ClientToContextKey());
		
		disqueConnectionCont = new DisqueConnectionController(settings.ClientToContextKey());
		disqueConnectionCont.SetContext(sContext);
	}
}
