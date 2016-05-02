using UnityEngine;
using System.Collections;
using WebSocketControl;
using System.Collections.Generic;
using System;

public class OnExecute : MonoBehaviour {
	private const string playerId = "100";
	WebSocketConnectionController webSocketCont;
	
	int count = 0;
	
	// Use this for initialization
	void Start () {		
		// クライアント接続を開始して、接続できたら云々。
		WebSocketConnectionController.InitWebSocketConnection(
			new Dictionary<string, string>{
				{"playerId", playerId}
			},//Dictionary<string, string> customHeaderKeyValues, 
			"rolePlayAgent", // string agent,
			() => {
				Debug.LogError("connected!");
			},// Action connected, 
			(List<byte[]> datas) => {
				Debug.LogError("data incomming!");
			},// Action<List<byte[]>> onBinaryMessage,
			(connectionFailedReason) => {
				Debug.LogError("connection failed, connectionFailedReason:" + connectionFailedReason);
			},// Action<string> connectionFailed, 
			(disconnectedReason) => {
				Debug.LogError("disconnected, reason:" + disconnectedReason);
			},// Action<string> disconnected,
			false,// bool autoReconnect,
			() => {}// Action reconnected
		);
	}
	
	
	// Update is called once per frame
	void Update () {
	
	}
	
	public void OnApplicationQuit () {
		WebSocketConnectionController.CloseCurrentConnection();
	}
}
