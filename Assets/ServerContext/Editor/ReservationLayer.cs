using System;
using System.Collections.Generic;
using XrossPeerUtility;
/**
	第二レイヤ
	予約を元にConnectionをゲームへと送り込む。
	ただのゲート。
*/
public class ReservationLayer {
	private readonly string reservationLayerId;
	
	private GameContextLayer gameLayer;
	
	public ReservationLayer (Action<string, byte[]> publish) {
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "とりあえず通過できるtokenとして特定のplayerIdを直書きしてある。");
		
		var reservedPlayerIds = new List<string>{"_empty_",};
		/*
			acceptable id is 100 ~ 199
		*/
		for (var i = 100; i < 200; i++) {
			reservedPlayerIds.Add(i.ToString());
		}
		
		reservationLayerId = Guid.NewGuid().ToString();
		gameLayer = new GameContextLayer(reservedPlayerIds, publish);
	}
	
	public void EnqueueOnConnect (string connectionId, string token) {
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "connectionIdとplayerDataが揃った状態でくる。ので、ここで照会を行ってしまおう。token:" + token);
		
		var playerId = token;
		XrossPeer.Log("playerId:" + playerId + " connectionId:" + connectionId);
		
		// set connectionId to reserved playerId.
		var succeeded = gameLayer.SetConnectionIdOfPlayerId(playerId, connectionId);
		if (!succeeded) return;
		
		if (playerId == "_empty_") {
			XrossPeer.Log("接続時にプレイヤーIDが空のユーザーが接続してきた。追い返すようにしてある。");
			return;
		}
		
		var data = new Commands.OnConnected(playerId).ToData();
		if (true) gameLayer.EnqueOnReceive(connectionId, data);
	}
	
	public void EnqueueOnMessage (string connectionId, byte[] data) {
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "とりあえずすべてのconnectionIdに対して、このreserveレイヤに登録があった、みたいなみなしをしてうけいれる。実際にはReservationLayerが複数のGameLayerをもっていて、特定の情報を元にGameContextLayerへとメッセージをふり分ける。");
		if (true) gameLayer.EnqueOnReceive(connectionId, data);
	}
	public void EnqueueOnDisconnect (string connectionId, string token, string reason) {
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "tokenそのまま使ってるんで、このままの構造だと、他人が偽って他プレイヤーの通信切断できちゃうな。プレイヤーしかしらないパラメータを使ってplayerIdを読みだす仕組みをつくらんとな。 具体的にはtokenが");
		
		var playerId = token;
		var data = new Commands.OnDisconnected(playerId, reason).ToData();
		
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "disconnect. とりあえずすべてのconnectionIdに対して、このreserveレイヤに登録があった、みたいなみなしをしてうけいれる。");
		if (true) {
			// remove connectionId from reserved playerId.
			var succeeded = gameLayer.DiscardConnectionIdOfPlayerId(playerId);
			if (!succeeded) XrossPeer.Log("playerId:" + playerId + " のconnectionの廃棄に失敗した。存在しないプレイヤーからの切断っぽい。");
			
			/*
				すでに切断されているので、このプレイヤーへの通信はこの時点で不可能。
			*/
			gameLayer.EnqueOnDisconnect(playerId, data);
		}
	}
}