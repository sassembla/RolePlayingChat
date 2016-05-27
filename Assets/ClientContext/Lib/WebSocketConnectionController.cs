using UnityEngine;

using System;
using System.Collections.Generic;

using UniRx;
using WebuSocketCore;


/**
	connect to WebSocket server and get push from the server.
	all received datas will appear in main thread.
*/
namespace WebSocketControl {
	public class WebSocketConnectionController {
		public static WebuSocketClient webuSocket;

		public static string WEBSOCKET_ENTRYPOINT;
		
		
		private static int RECONNECTION_MILLISEC = 1000;
		
		public static Queue<byte[]> binaryQueue = new Queue<byte[]>();
		
		static WebuSocket w2;
		
		public static void InitWebSocketConnection (
			Dictionary<string, string> customHeaderKeyValues, 
			string agent,
			Action connected, 
			Action<List<byte[]>> onBinaryMessage,
			Action<string> connectionFailed, 
			Action<string> disconnected,
			bool autoReconnect,
			Action reconnected
		) {
			var keySetting = (StandardAssetsConnectorSettings)ScriptableObject.CreateInstance("StandardAssetsConnectorSettings");
			WEBSOCKET_ENTRYPOINT = keySetting.DomainKey() + keySetting.ClientKey();
			
			Observable.EveryUpdate().Subscribe(
				_ => {
					if (0 < binaryQueue.Count) {
						List<byte[]> messages;
						lock (binaryQueue) {
							messages = new List<byte[]>(binaryQueue);
							binaryQueue.Clear();
						}
						onBinaryMessage(messages);
					}
				}
			);
			
			w2 = new WebuSocket(
				WEBSOCKET_ENTRYPOINT, 
				1024 * 100, 
				() => {
					Debug.LogError("connected,");
					
					for (var i = 0; i < 100; i++) {
						w2.Ping(
							() => {
								// Debug.LogError("receive pong.");
							}
						);
					}
					
					// w2.Ping(
					// 	() => {
					// 		Debug.LogError("receive pong2.");
					// 	}
					// );
					
					// var a = "";
					// MainThreadDispatcher.Post(
					// 	(b) => {
					// 		connected();
					// 	},
					// 	a
					// );
				}, 
				(Queue<byte[]> datas) => {
					lock (binaryQueue) {
						while (0 < datas.Count) binaryQueue.Enqueue(datas.Dequeue());
					}
				}, 
				() => {
					Debug.LogError("pingされたぞ〜");
				}, 
				(string closeReason) => {
					Debug.LogError("closeReason:" + closeReason);
					var a = "";
					MainThreadDispatcher.Post(
						(b) => {
							// run on main thread.
						},
						a
					);
				}, 
				(string errorReason, Exception e) => {
					Debug.LogError("errorReason:" + errorReason);
					var a = "";
					MainThreadDispatcher.Post(
						(b) => {
							// run on main thread.
						},
						a
					);
				}, 
				customHeaderKeyValues
			);
			
			// webuSocket = new WebuSocketClient(
			// 	WEBSOCKET_ENTRYPOINT,
			// 	() => {
			// 		var a = "";
			// 		MainThreadDispatcher.Post(
			// 			(b) => {
			// 				connected();
			// 			},
			// 			a
			// 		);
			// 	},
			// 	(Queue<byte[]> datas) => {
			// 		lock (binaryQueue) {
			// 			while (0 < datas.Count) binaryQueue.Enqueue(datas.Dequeue());
			// 		}
			// 	},
			// 	() => {
			// 		Debug.LogError("pingされたぞ〜");
			// 	},
			// 	(string closeReason) => {
			// 		Debug.LogError("closeReason:" + closeReason);
			// 		var a = "";
			// 		MainThreadDispatcher.Post(
			// 			(b) => {
			// 				// run on main thread.
			// 			},
			// 			a
			// 		);
			// 	},
			// 	(string errorReason, Exception e) => {
			// 		Debug.LogError("errorReason:" + errorReason);
			// 		var a = "";
			// 		MainThreadDispatcher.Post(
			// 			(b) => {
			// 				// run on main thread.
			// 			},
			// 			a
			// 		);
			// 	},
			// 	0,
			// 	customHeaderKeyValues
			// );
		}
		
		public static void SendCommandAsync (byte[] command) {
			if (webuSocket != null) webuSocket.Send(command);
		}
		
		public static void CloseCurrentConnection () {
			if (webuSocket != null) webuSocket.CloseSync();
		}
	}
}