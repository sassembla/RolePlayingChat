using UnityEngine;

using D = System.Diagnostics;
using System;
using System.Collections.Generic;

using UniRx;
using WebuSocketCore;
using System.Text;
using XrossPeerUtility;


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
		
		static long start = 0;
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
			XrossPeer.SetupLog("client.log");
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
				1024 * 100,
				() => {
					var s2 = DateTime.Now.Ticks;
					// Debug.LogError("start1 date:" + DateTime.Now.Ticks);
					w2.Ping(
						() => {
							// これが、nginxまでの往復距離。多分、遅延時間が最大になる要因。
							/*
								おっ結構かかるな、、、往復で(tick = 100 nano sec, 1 nano sec = 1/1000,000,000 sec)
								
								ElapsedTicks
								
								129,200
								122,100
								
								で、upstreamは、
								70,050
								
								で、打って変わってDateTime.Now.Ticks
								
								wsping:
									start1	636008591925501940
									end1	636008591925660450
														158,510 tick.
									
								serverPing:						
									start2	636008591925619370	
									disq-r	636008591925768230	148860(send -> disque -> server)あ、だいたいTCP ping往復と似たような数字出てるな、、
									serv-r	636008591925885340	117110(server to push)
									end2	636008591926057000	171660
														437,630 tick.
														
									このうち、ping部分は 158,510なので、
														279,120 tick.
								で、
								
								There are 10,000 ticks in a millisecond ってことなんで、
								0.001 sec = 1msec に、10,000tickが入る。ので、
								
								1tickは1/10,000 msec = 1/10,000,000 sec。
								1tick = 一千万分の1秒。
								
								で、
								
								1f = 60f/s = 0.016 sec.
								
								wspingは 158,510 tick -> 0.015851 sec
								
								Disqueまわりで無駄に食っている時間は、
									   279,120 tick -> 0.027912 sec
								
								んーサーバ内で1~2f食ってるの無駄だなー、、でもどうせここからさらにサーバ側のフレームレートあるから食うんだよな、、
								まあnginxのフレームレートが存在するのと、Disqueのメッセージキュー機構が含まれてるんで、不思議ではない。
								Disqueの往復を挟むとこんな感じになるのか。
								
								ちなみにupdate:
									052,320
									217,850
									-> 165,530
								
								結論:間にDisque入れることで往復で1fくらい余計にかかる。というかサーバ内でも結構時間かかる。
								
								この時間を加味する必要があるかな。
								この時間を加味する必要がないほうがいいな〜〜〜
								
								計測できるけどな〜〜
								
								想定できる遅延は、200ms x 2 とかで、
								これが230msとかになる。
								
								パーセンテージではなく固定なんだけど、うーーん、、まあなるべく高速に返すのは理想として。
								計測しておけると、安定した効果を期待できる。
								
								ConnectionServerにおける理想系みたいなのも持っておくかな。
								nginx-luajitは結構有能なんで、この派生系みたいなやつ。
							*/
							XrossPeer.Log("ping:" + (DateTime.Now.Ticks - s2));
						}
					);
					
					// XrossPeer.Log("start:" + DateTime.Now.Ticks);
					// start = DateTime.Now.Ticks;
					// w2.Send(new Commands.BaseData(Commands.CommandEnum.Ping, "here!").ToData());// これがあるとエラーが出る、っていう。型のマッチングエラーぽい？
					
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
						while (0 < datas.Count) {
							var data = datas.Dequeue();
							var bytes = new byte[data.Count];
							Buffer.BlockCopy(data.Array, data.Offset, bytes, 0, data.Count);
							var e = Commands.ReadCommandAndSourceId(bytes);
							if (e.command == Commands.CommandEnum.Ping) {
								// XrossPeer.Log("end2 date:" + (DateTime.Now.Ticks - start));
							}
							binaryQueue.Enqueue(data);
						}
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