using XrossPeerUtility;

using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using DisquuunCore;
using DisquuunCore.Deserialize;

/**
	このレイヤを分解する。
	Disquuunはゲーム単位で保持しても問題無いし、変換レイヤはstaticで存在していい感じになる。
*/
public class ConnectionServerTransformLayer {
	private ServerContext context;
	private Disquuun disquuun;
	
	public ConnectionServerTransformLayer (string contextQueueIdentity) {
		disquuun = new Disquuun(
			"127.0.0.1", 7711, 1024 * 100, 3, 
			conId => {
				// enable adding job to Disque by swapping publisher method.
				context.Setup(Publish);
				
				// start getting job from disque then fastack all automatically.
				disquuun.GetJob(new string[]{contextQueueIdentity}, "count", 1000).Loop(
					(command, data) => {
						var jobs = DisquuunDeserializer.GetJob(data);
						
						var jobIds = jobs.Select(jobData => jobData.jobId).ToArray();
						var jobDatas = jobs.Select(jobData => jobData.jobData).ToList();
						
						/*
							fast ack all.
						*/
						disquuun.FastAck(jobIds).Async((command2, data2) => {});
						
						InputDatasToContext(jobDatas);
						return true;
					}
				);
			}
		);
	}
	
	public void Disconnect () {
		if (disquuun != null) disquuun.Disconnect();
	}

	public void SetContext (ServerContext context) {
		this.context = context;
	}
	
	public void Publish (string targetConnectionId, byte[] data) {
		disquuun.AddJob(targetConnectionId, data).Async(
			(command, result) => {
				// これなんかハンドルしたいかなあ。
			}
		);
	}
	
	
	
	// こっからフィルタ。
	/*
		フィルタは、staticでいいんで、どっかにコピーして成立させよう。
		
		突きあわせレイヤーだ。
		ConnectionServerと、ServerContextと、DisqueConnectionControllerの三つ巴ポイント。
		
		疎結合にしておけると良い感じなので、このレイヤで何かすべき、っていうレイヤは上位に持っていくと良い気がする。
		・ServerContext				ゲーム本体、最小単位はDisqueのキューIdになる。データの受け入れと出力をする。
		・DisqueConnectionController	Disqueの管理、送信と受信のハンドラを持つ。
		・ServerController			
	*/
	
	// webSocket server state for each connection. syncronized to nginx-lua-client.lua code.
	public const char STATE_CONNECT			= '1';
	public const char STATE_STRING_MESSAGE		= '2';
	public const char STATE_BINARY_MESSAGE		= '3';
	public const char STATE_DISCONNECT_INTENT	= '4';
	public const char STATE_DISCONNECT_ACCIDT	= '5';
	public const char STATE_DISCONNECT_DISQUE_ACKFAILED = '6';
	public const char STATE_DISCONNECT_DISQUE_ACCIDT_SENDFAILED = '7';


	public const int CONNECTION_ID_LEN = 36;// 65D76DEE-0E68-424E-A18F-6D2CC9656FB3
	
	/**
		受け取ったjobを解釈、contextへと入力する。APIレイヤーはserverContext側にあるので、データを扱うこの辺は変更なしで行けるはず。
	*/
	public void InputDatasToContext (List<byte[]> datas) {
		// XrossPeer.Log("datas received. datas:" + datas.Count);
		/*
			messageとして受け取ったjobを、list化して読み込む。
		*/
		foreach (var dataArray in datas) {
			var len = dataArray.Length;
			if (len < 1/*state param*/ + CONNECTION_ID_LEN/*connectionId*/) {
				var invalidMessage = Encoding.ASCII.GetString(dataArray);
				XrossPeer.Log("illigal format invalidMessage1:" + invalidMessage);
				continue;
			}
			
			var state = (char)dataArray[0];

			// dataArray[2-38] is connectionId, length = definitely CONNECTION_ID_LEN.
			var connectionId = Encoding.ASCII.GetString(dataArray, 1, CONNECTION_ID_LEN);
			
			switch (state) {
				case STATE_CONNECT: {
					XrossPeer.Log("STATE_CONNECT");
					if (1 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (1 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (1 + CONNECTION_ID_LEN), data, 0, dataLen);
						context.OnConnected(connectionId, data);	
					}
					break;
				}

				case STATE_STRING_MESSAGE: {
					// if (1 + CONNECTION_ID_LEN < len) {
					// 	var data = Encoding.UTF8.GetString(dataArray, 2 + CONNECTION_ID_LEN, len - (2 + CONNECTION_ID_LEN));
					// 	context.OnMessage(connectionId, data);
					// }
					break;
				}

				case STATE_BINARY_MESSAGE: {
					if (1 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (1 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (1 + CONNECTION_ID_LEN), data, 0, dataLen);
						context.OnMessage(connectionId, data);
					}
					break;
				}

				case STATE_DISCONNECT_INTENT: {
					if (1 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (2 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (1 + CONNECTION_ID_LEN), data, 0, dataLen);
						XrossPeer.Log("client closed");
						context.OnDisconnected(connectionId, data, "intentional disconnect.");
					}
					break;
				}

				case STATE_DISCONNECT_ACCIDT: {
					if (1 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (2 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (1 + CONNECTION_ID_LEN), data, 0, dataLen);
						
						context.OnDisconnected(connectionId, data, "accidential disconnect.");
					}
					break;
				}
				case STATE_DISCONNECT_DISQUE_ACKFAILED: {
					if (1 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (1 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (1 + CONNECTION_ID_LEN), data, 0, dataLen);
						
						context.OnDisconnected(connectionId, data, "accidential disconnect.");
					}
					break;
				}
				case STATE_DISCONNECT_DISQUE_ACCIDT_SENDFAILED: {
					if (1 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (1 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (1 + CONNECTION_ID_LEN), data, 0, dataLen);
						
						context.OnDisconnected(connectionId, data, "send failed to client. disconnect.");
					}
					break;
				}

				default: {
					XrossPeer.Log("undefined websocket state:" + state);
					break;
				}
			}
		}
	}
}
