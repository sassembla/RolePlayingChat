﻿using XrossPeerUtility;

using UnityEngine;
using UnityEditor;

using System;
using System.IO;


[InitializeOnLoad] public class ServerInitializer {
	[MenuItem ("ServerInitializer/Regenerate Private Client Key", false, 1)] public static void RegenerateClientRandomKey () {
  		var settings = (StandardAssetsConnectorSettings)ScriptableObject.CreateInstance("StandardAssetsConnectorSettings");
		settings.GeneratePrivateClientKey();
  	}
  	private static ServerInitializer initializer;
	
	static ServerInitializer () {// called by Unity.
		initializer = new ServerInitializer();
	}
	
	
	public ServerInitializer () {
		XrossPeer.SetupLog(Path.Combine(Directory.GetParent(Application.dataPath).ToString(), "server.log"));
		
		// Setup();
		DisquuunTests.Start();
		
		EditorApplication.playmodeStateChanged += DetectPlayStart;
		EditorApplication.update += DetectCompileStart;
	}
	
	private void DetectPlayStart () {
		if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode) {
			EditorApplication.playmodeStateChanged -= DetectPlayStart;
			initializer.Teardown();
			DisquuunTests.Stop();
		}
	}
	
	private void DetectCompileStart () {
		if (EditorApplication.isCompiling) {
			EditorApplication.update -= DetectCompileStart;
			
			initializer.Teardown();
			DisquuunTests.Stop();
		}
	}
	
	private ServerContext sContext;
	private ConnectionServerTransformLayer transformLayer;
	
	
	public void Setup () {
		XrossPeer.Log("\n\n");
		XrossPeer.Log("----------");
		XrossPeer.Log("initializing server context....");
		XrossPeer.Log("----------");
		
		var settings = (StandardAssetsConnectorSettings)ScriptableObject.CreateInstance("StandardAssetsConnectorSettings");
		
		sContext = new ServerContext(settings.ClientToContextKey());
		
		transformLayer = new ConnectionServerTransformLayer(settings.ClientToContextKey());
		transformLayer.SetContext(sContext);
	}
	
	public void Teardown () {
		XrossPeer.Log("\n\n");
		XrossPeer.Log("----------");
		XrossPeer.Log("teardown server context....");
		XrossPeer.Log("----------");
		if (sContext != null) sContext.Teardown();
		if (transformLayer != null) transformLayer.Disconnect();
	}
}
