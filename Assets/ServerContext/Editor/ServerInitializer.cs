using XrossPeerUtility;

using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;


/*
	このクラス自体がUnityに依存しているので、なんかうまい抽象化を考えないとな
*/
[InitializeOnLoad] public class ServerInitializer {
	static ServerContext sContext;

	static DisqueSharp disqueSharp;
	
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
		
		sContext = new ServerContext();
		switch (settings.QueueType()) {
			case "disque": {
				var connectionId = Guid.NewGuid().ToString();
				
				disqueSharp = new DisqueSharp(
					connectionId,
					"127.0.0.1", 
					7711,
					102400,
					// このへんの露出してるのもまあいらなくなるよなっていう感じ。Contextを飲み込めば。ただ、
					// 特定の関数の受け取りテーブルを書けばいいようにしておくと、沈めた時に楽になりそうな予感。
					connectedConId => {
						XrossPeer.Log("connectedConId:" + connectedConId + " ここから適当な送信コード");
						
						int counter = 0;			
						
						Func<bool> UpdateSending = () => {
							// Debug.LogError("connected!");
							if (counter == 0) {
								disqueSharp.AddJob("testQ", new byte[10]{0,1,2,3,4,5,6,7,8,9}, 0);
							}
							
							if (counter == 10) {
								// disqueSharp.GetJob(new string[]{"testQ"});
							}
							
							// if (counter == 20) {
							// 	disqueSharp.AckJob();
							// }
							
							// if (counter == 30) {
							// 	disqueSharp.AckJob();
							// }
							
							// if (counter == 40) {
							// 	disqueSharp.GetJob(new string[]{"testQ"});
							// }
							
							// if (counter == 50) {
							// 	disqueSharp.GetJob(new string[]{"testQ"});
							// }
							
							
							
							if (counter == 160) {// 複数件がいっぺんにくるケース
								disqueSharp.Info();
								disqueSharp.Info();
								disqueSharp.Info();
								disqueSharp.Info();
								disqueSharp.Info();
								disqueSharp.Info();
							}
							
							if (counter == 170) {
								disqueSharp.Hello();
							}
							
							
							// if (counter == 300) {
							// 	disqueSharp.Info();
							// 	XrossPeer.Log("connectedConId:" + connectedConId + " ここまで適当な送信コード");
							// }
							
							counter++;
							return true;
						};
						
						SetupUpdater("dddd", UpdateSending);
					},
					(command, bytes) => {
						XrossPeer.Log("data received:" + command + " bytes:" + bytes.Length);
					},
					e => {
						XrossPeer.LogError("e:" + e);
					},
					disconnectedConId => {
						XrossPeer.LogError("disconnectedConId:" + disconnectedConId);
					}
				);
				break;
			}

			default: {
				XrossPeer.Log("undefined queue system1:" + settings.QueueType());
				break;
			}
		}
		
		
		EditorApplication.update += DetectCompileStart;
	}
	
	
	
	
	
	private static void DetectCompileStart () {
		if (EditorApplication.isCompiling) {
			EditorApplication.update -= DetectCompileStart;
			
			disqueSharp.Disconnect();
			sContext.Teardown();
		}
	}
	
	
	/*
		不要になる。
	*/
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
