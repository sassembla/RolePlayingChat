using XrossPeerUtility;

using System;
using System.Text;
using DisquuunCore;

public class ServerContext {
	
	private readonly string serverContextId;

	private ReservationLayer reservationLayer;
	
	private Disquuun disquuun;

	public ServerContext (string serverQueueId) {
		serverContextId = Guid.NewGuid().ToString();
		
		XrossPeer.Log("server generated! serverContextId:" + serverContextId + " serverQueueId:" + serverQueueId);
		
		// Updater queueGetJobber = null;
		
		// ちょうどDisqueConnectionControllerとDisquuunのあいだをいったりきたりしてるんで齟齬がある。
		var disqueId = Guid.NewGuid().ToString();
		// disquuun = new Disquuun(
		// 	disqueId,
		// 	"127.0.0.1", 
		// 	7711,
		// 	102400,
		// 	connectedConId => {
		// 		Action<string, byte[]> Send = (string connectionId, byte[] data) => {
		// 			disquuun.AddJob(connectionId, data);
		// 		};
				
		// 		Setup(Send);
				
		// 		XrossPeer.Log("同期的にコンテキストの用意が終わったつもり。 ほんとはいろんな接続があるはず。");
		// 		queueGetJobber = new Updater(
		// 			"disquuunGetJobber", 
		// 			() => {
		// 				disquuun.GetJob(new string[]{serverQueueId}, "COUNT", 1000, "NOHANG");
		// 				return true;
		// 			}
		// 		);
				
		// 	},
		// 	(command, byteDatas) => {
		// 		switch (command) {
		// 			case Disquuun.DisqueCommand.INFO: {
		// 				var stringData = byteDatas[0];
		// 				var info = Encoding.UTF8.GetString(stringData.bytesArray[0], 0, stringData.bytesArray[0].Length);
		// 				XrossPeer.Log("info:" + info);
		// 				break;
		// 			}
		// 			case Disquuun.DisqueCommand.GETJOB: {
		// 				var jobIds = new List<string>();
		// 				foreach (var bytes in byteDatas) {
		// 					var jobId = Encoding.UTF8.GetString(bytes.bytesArray[0]);
		// 					jobIds.Add(jobId);
							
		// 					ParseData(bytes.bytesArray[1]);
		// 				}
						
		// 				if (jobIds.Any()) disquuun.FastAck(jobIds.ToArray());
		// 				break;
		// 			}
		// 			case Disquuun.DisqueCommand.FASTACK: {
		// 				// do nothing.
		// 				break;
		// 			}
		// 			default: {
		// 				break;
		// 			}
		// 		}
		// 	},
		// 	(failedCommand, reason) => {
		// 		XrossPeer.LogError("failedCommand:" + failedCommand + " reason:" + reason);
		// 	},
		// 	e => {
		// 		XrossPeer.LogError("Disque error:" + e);
		// 		if (queueGetJobber != null) queueGetJobber.Quit();
		// 	},
		// 	disconnectedConId => {
		// 		XrossPeer.Log("Disque disconnected:" + disqueId);
		// 	}
		// );
	}
	
	private const char HEADER_STRING	= 's';
	private const char HEADER_BINARY	= 'b';
	private const char HEADER_CONTROL	= 'c';

	// webSocket server state for each connection. syncronized to nginx-lua client.lua code.
	private const char STATE_CONNECT			= '1';
	private const char STATE_STRING_MESSAGE		= '2';
	private const char STATE_BINARY_MESSAGE		= '3';
	private const char STATE_DISCONNECT_INTENT	= '4';
	private const char STATE_DISCONNECT_ACCIDT	= '5';
	private const char STATE_DISCONNECT_DISQUE_ACKFAILED = '6';
	private const char STATE_DISCONNECT_DISQUE_ACCIDT_SENDFAILED = '7';


	private const int CONNECTION_ID_LEN = 36;// 65D76DEE-0E68-424E-A18F-6D2CC9656FB3
	
	private void ParseData (byte[] dataArray) {
		var len = dataArray.Length;
		if (len < 1/*s or b or c*/ + 1/*state param*/ + CONNECTION_ID_LEN/*connectionId*/) {
			var invalidMessage = Encoding.ASCII.GetString(dataArray);
			XrossPeer.Log("illigal format invalidMessage1:" + invalidMessage);
			return;
		}

		var header = (char)dataArray[0];
		switch (header) {
			case HEADER_CONTROL:
			case HEADER_STRING:
			case HEADER_BINARY: {
				break;
			}
			default: {
				var invalidMessage = Encoding.ASCII.GetString(dataArray);
				XrossPeer.Log("illigal format invalidMessage2:" + invalidMessage);
				return;
			}
		}
		
		var state = (char)dataArray[1];

		// dataArray[2-38] is connectionId, length = definitely CONNECTION_ID_LEN.
		var connectionId = Encoding.ASCII.GetString(dataArray, 2, CONNECTION_ID_LEN);
		
		switch (state) {
			case STATE_CONNECT: {
				if (2 + CONNECTION_ID_LEN < len) {
					var dataLen = len - (2 + CONNECTION_ID_LEN);
					var data = new byte[dataLen];
					Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
					OnConnected(connectionId, data);	
				}
				break;
			}

			case STATE_STRING_MESSAGE: {
				// ignored.
				break;
			}

			case STATE_BINARY_MESSAGE: {
				if (2 + CONNECTION_ID_LEN < len) {
					var dataLen = len - (2 + CONNECTION_ID_LEN);
					var data = new byte[dataLen];
					Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
					OnMessage(connectionId, data);
				}
				break;
			}

			case STATE_DISCONNECT_INTENT: {
				if (2 + CONNECTION_ID_LEN < len) {
					var dataLen = len - (2 + CONNECTION_ID_LEN);
					var data = new byte[dataLen];
					Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
					
					OnDisconnected(connectionId, data, "intentional disconnect.");
				}
				break;
			}

			case STATE_DISCONNECT_ACCIDT: {
				if (2 + CONNECTION_ID_LEN < len) {
					var dataLen = len - (2 + CONNECTION_ID_LEN);
					var data = new byte[dataLen];
					Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
					
					OnDisconnected(connectionId, data, "accidential disconnect.");
				}
				break;
			}
			case STATE_DISCONNECT_DISQUE_ACKFAILED: {
				if (2 + CONNECTION_ID_LEN < len) {
					var dataLen = len - (2 + CONNECTION_ID_LEN);
					var data = new byte[dataLen];
					Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
					
					OnDisconnected(connectionId, data, "accidential disconnect.");
				}
				break;
			}
			case STATE_DISCONNECT_DISQUE_ACCIDT_SENDFAILED: {
				if (2 + CONNECTION_ID_LEN < len) {
					var dataLen = len - (2 + CONNECTION_ID_LEN);
					var data = new byte[dataLen];
					Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
					
					OnDisconnected(connectionId, data, "send failed to client. disconnect.");
				}
				break;
			}

			default: {
				XrossPeer.Log("undefined websocket state:" + state);
				break;
			}
		}
	}
	Action<string, byte[]> Send;
	public void Setup (Action<string, byte[]> Send) {
		XrossPeer.Log("server ready:" + serverContextId);
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "リセットを兼ねることはしない方が良いんだろうか。");
		
		// 仮の、ゲームに参加するconnectionIdを保持しておくレイヤ
		reservationLayer = new ReservationLayer(Send);
		this.Send = Send;
	}
	
	/**
		ServerContextの終了手続き
	*/
	public void Teardown () {
		XrossPeer.TimeAssert(Develop.TIME_ASSERT, "ContextのTeardown処理、なんか必要かな、、");
	}
	
	
	public void OnConnected (string connectionId, byte[] data) {
		XrossPeer.Log("OnConnected!");
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
		
		if (reservationLayer != null) reservationLayer.EnqueueOnConnect(connectionId, playerIdString);
	}
	
	public void OnMessage (string connectionId, byte[] data) {
		var playerIdString = Encoding.UTF8.GetString(data);
		reservationLayer.EnqueueOnMessage(connectionId, data);
	}

	public void OnDisconnected (string connectionId, byte[] data, string reason) {
		var playerIdString = Encoding.UTF8.GetString(data);
		if (reservationLayer != null) reservationLayer.EnqueueOnDisconnect(connectionId, playerIdString, reason);
	}
}
