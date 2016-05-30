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
		// public static WebuSocketClient webuSocket;

		public static string WEBSOCKET_ENTRYPOINT;
		
		
		private static int RECONNECTION_MILLISEC = 1000;
		
		public static Queue<ArraySegment<byte>> binaryQueue = new Queue<ArraySegment<byte>>();
		
		static WebuSocket w2;
		
		public static void InitWebSocketConnection (
			Dictionary<string, string> customHeaderKeyValues, 
			string agent,
			Action connected, 
			Action<Queue<ArraySegment<byte>>> OnBinaryMessage,
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
						Queue<ArraySegment<byte>> messages;
						lock (binaryQueue) {
							messages = binaryQueue;
							OnBinaryMessage(messages);
							binaryQueue.Clear();
						}
					}
				}
			);
			
			w2 = new WebuSocket(
				WEBSOCKET_ENTRYPOINT,
				1, 
				// 1024 * 100,
				() => {
					var a = "";
					MainThreadDispatcher.Post(
						(b) => {
							connected();
						},
						a
					);
				}, 
				(Queue<ArraySegment<byte>> datas) => {
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
		}
		
		public static void SendCommandAsync (byte[] command) {
			if (w2 != null) w2.Send(command);
		}
		
		public static void CloseCurrentConnection () {
			if (w2 != null) w2.Disconnect(true);
		}
	}
}