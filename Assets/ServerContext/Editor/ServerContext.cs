using XrossPeerUtility;

using System;
using System.Linq;
using System.Collections.Generic;

using System.IO;
using System.Text;

public class ServerContext {
	
	private readonly string serverContextId;

	private ReservationLayer reservationLayer;

	public ServerContext () {
		serverContextId = Guid.NewGuid().ToString();
		XrossPeer.Log("server generated! serverContextId:" + serverContextId);
		
		// Updater(OnUpdate);//このレイヤで何か欲しいものってあるかな、、
	}
	
	public void Setup () {
		XrossPeer.Log("server ready:" + serverContextId);
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "リセットを兼ねることはしない方が良いんだろうか。");
		
		// 仮の、ゲームに参加するconnectionIdを保持しておくレイヤ
		reservationLayer = new ReservationLayer(PublishTo);
	}
	
	/**
		ServerContextの終了手続き
	*/
	public void Teardown () {
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "ContextのTeardown処理、なんか必要かな、、");
	}
	
	
	
	public void OnConnected (string connectionId, byte[] data) {
		XrossPeer.Log("OnConnected! connectionId:" + connectionId);
		/*
			接続時にidentityを確立する手段が2つ考えられて、
			1.接続時にconnectionServer側で予約と付き合わせてなんとかする
			2.このレイヤーで予約と付き合わせてなんとかする
			責務分解的には、接続してきたらすぐキャッシュと照合、っていうので良い気はするんだけど。
			どっちにしてもconnectedで情報が必要なので、reservationレイヤーでそれを受け止めるのは悪くない。
			
			ServerContextはゲームに集中させたい。
			ConnectionServerはコネクションに集中させたい。
			うーーん、、別のContextがあってそっちにつなぎにいけばいいのか。domain的にはConnection側だな、、
		*/
		var playerIdString = Encoding.UTF8.GetString(data);
		
		reservationLayer.EnqueueOnConnect(connectionId, playerIdString);
		 
		// send per frame test.
		// for (var i = 0; i < 100000; i++) {
		// 	var data = new Commands.GameData(i);
		// 	PublishTo(data, new string[]{connectionId});
		// }
	}

	public void OnMessage (string connectionId, string data) {}
	
	public void OnMessage (string connectionId, byte[] data) {
		var playerIdString = Encoding.UTF8.GetString(data);
		reservationLayer.EnqueueOnMessage(connectionId, data);
	}

	public void OnDisconnected (string connectionId, byte[] data, string reason) {
		var playerIdString = Encoding.UTF8.GetString(data);
		reservationLayer.EnqueueOnDisconnect(connectionId, playerIdString, reason);
	}
	
	/**
		publisher methods
	*/
	private Action<object, string> PublishTo = NotYetReady;
	public void SetPublisher (Action<object, string> publisher) {
		PublishTo = publisher;
		Setup();
	}

	private static void NotYetReady (object obj, string connectionId) {
		XrossPeer.Log("not yet publishable.");
	}
}