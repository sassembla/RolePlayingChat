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

	static Disquuun disquuun;
	
	static ServerInitializer () {
		XrossPeer.SetupLog(Path.Combine(Directory.GetParent(Application.dataPath).ToString(), "server.log"));
		Setup();
	}
	
	public static string gotJobId;
	
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
				
				disquuun = new Disquuun(
					connectionId,
					"127.0.0.1", 
					7711,
					102400,
					// このへんの露出してるのもまあいらなくなるよなっていう感じ。Contextを飲み込めば。ただ、
					// 特定の関数の受け取りテーブルを書けばいいようにしておくと、沈めた時に楽になりそうな予感。
					connectedConId => {
						RunTests(connectedConId);
					},
					(command, bytes0, bytes1) => {
						if (bytes1 != null) XrossPeer.Log("data received:" + command + " bytes0:" + bytes0.Length + " bytes1:" + bytes1.Length);
						else XrossPeer.Log("data received:" + command + " bytes0:" + bytes0.Length);
						
						switch (command) {
							case Disquuun.DisqueCommand.GETJOB: {
								var jobIdStr = Encoding.UTF8.GetString(bytes0, 0, bytes0.Length);
								// XrossPeer.Log("jobIdStr:" + jobIdStr);
								
								gotJobId = jobIdStr;
								break;
							}
							case Disquuun.DisqueCommand.HELLO: {
								var info = Encoding.UTF8.GetString(bytes0, 0, bytes0.Length);
								XrossPeer.Log("info:" + info);
								break;
							}
							default: {
								// ignored
								break;
							}
						} 
					},
					(command, bytes) => {
						XrossPeer.Log("data failed:" + command + " bytes:" + bytes.Length);
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
	
	private static void RunTests (string connectedConId) {
		XrossPeer.Log("connectedConId:" + connectedConId + " ここから適当な送信コード");
						
		int counter = 0;			
		
		Func<bool> UpdateSending = () => {
			// Debug.LogError("connected!");
			{
				if (counter == 0) {
					disquuun.AddJob("testQ", new byte[10]{0,1,2,3,4,5,6,7,8,9}, 0);
				}
				if (counter == 10) {
					disquuun.GetJob(new string[]{"testQ"});
				}
				if (counter == 20) {
					disquuun.AckJob(new string[]{gotJobId});
				}
			}
			
			{
				if (counter == 30) {
					disquuun.AddJob("testV", new byte[10]{0,1,2,3,4,5,6,7,8,9}, 0);
				}
				if (counter == 40) {
					disquuun.GetJob(new string[]{"testV"});
				}
				if (counter == 50) {
					disquuun.FastAck(new string[]{gotJobId});
				}
			}
			
			// if (counter == 40) {
			// 	disqueSharp.GetJob(new string[]{"testQ"});
			// }
			
			// if (counter == 50) {
			// 	disqueSharp.GetJob(new string[]{"testQ"});
			// }
			
			
			
			if (counter == 160) {// 複数件がいっぺんにくるケース
				disquuun.Info();
				disquuun.Info();
				disquuun.Info();
				disquuun.Info();
				disquuun.Info();
				disquuun.Info();
			}
			
			if (counter == 170) {
				disquuun.Hello();
			}
			
			
			counter++;
			return true;
		};
		
		SetupUpdater("dddd", UpdateSending);
	}
	
	
	
	private static void DetectCompileStart () {
		if (EditorApplication.isCompiling) {
			EditorApplication.update -= DetectCompileStart;
			
			disquuun.Disconnect();
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
