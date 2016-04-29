using UnityEngine;

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.Linq;


/**
	Motivations
	
	・async default
		だいたい全部async。書いてなくてもasync。
		syncは無い。
		
	・receive per frame
		WebSocket接続後、
		socket.Receiveとsocket.Sendを1Threadベースで一元化する。
		複数箇所で同時にReceiveしない。また、Receiveに非同期動作を含まない。
	
	・2 threading
		thread1:
			serverからの受信データのqueueと、その後にclientからのqueuedデータの送付を行う。
			
		thread2:
			queueされた受信データを解析、消化する。
			
		遅くなったり詰まったりしても、各threadの特定のフレームが時間的に膨張して、結果他の処理が後ろ倒しになるだけ。
		
	・ordered operation
		外部からのrequestや、内部での状態変化などは、できる限りorderedな形で扱う。
		即時的に動くのはcloseくらい。
		
*/
namespace WebuSocket {
	
	public class WebuSocketClient {
		private static RNGCryptoServiceProvider randomGen = new RNGCryptoServiceProvider();
		
		public readonly string webSocketConnectionId;
		
		private Socket socket;
		private readonly Thread updater;
		
		private const string CRLF = "\r\n";
		private const int HTTP_HEADER_LINE_BUF_SIZE = 1024;
		private const string WEBSOCKET_VERSION = "13"; 
		
			
		public enum WSConnectionState : int {
			Opening,
			Opened,
			Closing,
			Closed
		}
		
		private Action OnPong = () => {
			// do nothing.
		};
		
		private enum WSOrder : int {
			Ping,
			Pong,
			CloseGracefully,
		}
		
		private Queue<WSOrder> stackedOrders = new Queue<WSOrder>();
		
		private Queue<byte[]> stackedSendingDatas = new Queue<byte[]>();
		
		private List<byte[]> receivedDataList = new List<byte[]>();
		
		private WSConnectionState state;
		
		public WebuSocketClient (
			string url,
			Action OnConnected,
			Action<Queue<byte[]>> OnMessage,
			Action<string> OnClosed,
			Action<string, Exception> OnError,
			int throttle=0,
			Dictionary<string, string> additionalHeaderParams=null
		) {
			Debug.LogWarning("wss and another features are not supported yet.");
			/*
				unsupporteds:
					wss,
					redirect,
					proxy,
					fragments(fin != 1) for sending & receiving,
					text,
					
			*/
			
			this.webSocketConnectionId = Guid.NewGuid().ToString();
			
			state = WSConnectionState.Opening;
			
			
			Queue<byte[]> messageQueue = new Queue<byte[]>();
			
			
			/*
				stack of data which received header and half of payload.
				this is local valuable and never access to this instance from outside of consumer thread.
			*/
			var stackedBytes = new byte[0];
			
			/*
				thread for process the queue of received data.
			*/
			var receivedDataQueueConsumer = Updater(
				throttle,
				"WebuSocket-consumer-thread",
				() => {
					lock (receivedDataList) {
						if (0 < receivedDataList.Count) {
							/*
								start concatinate.
								new data is 
									stacket bytes + new data bytes.
							*/
							var totalLength = 0;
							foreach (var data in receivedDataList) totalLength = totalLength + data.Length;
							
							// stack length of data which is stacked at previous run.
							var dataIndex = stackedBytes.Length;
							
							/*
								expand stackedBytes. head of this bytes is maybe empty or rest of past-frame data. keep these datas & update size. 
							*/
							Array.Resize(ref stackedBytes, dataIndex + totalLength);
							
							// read all incoming datas. adding to stackedBytes.
							foreach (var receivedData in receivedDataList) {
								Buffer.BlockCopy(receivedData, 0, stackedBytes, dataIndex, receivedData.Length);
								dataIndex = dataIndex + receivedData.Length;
							}
							
							// consume all received data.
							receivedDataList.Clear();
							
							
							/*
								start reading.
							*/
							
							var messageIndexies = WebSocketByteGenerator.GetIndexies(stackedBytes);
														
							for (var i = 0; i < messageIndexies.Count; i++) {
								var messageIndex = messageIndexies[i];
								
								switch (messageIndex.opCode) {
									case WebSocketByteGenerator.OP_PING: {
										StackOrder(WSOrder.Pong);
										break;
									}
									case WebSocketByteGenerator.OP_PONG: {
										if (OnPong != null) OnPong();
										break;
									}
									case WebSocketByteGenerator.OP_TEXT: {
										Debug.LogError("text data is ignored.");
										break;
									}
									case WebSocketByteGenerator.OP_BINARY: {
										messageQueue.Enqueue(stackedBytes.SubArray(messageIndex.start, messageIndex.length));
										break;
									}
									case WebSocketByteGenerator.OP_CLOSE: {
										if (OnClosed != null) OnClosed("closed by server.");
										Close();
										break;
									}
								}
							}
							
							
							uint lastDataIndex = 0;
							if (0 < messageIndexies.Count) lastDataIndex = messageIndexies[messageIndexies.Count-1].start + messageIndexies[messageIndexies.Count-1].length;
							
							// fill stackedBytes with rest of partial message data.
							// will be 0 < length if fragment exists. or just 0.
							var restLength = (uint)stackedBytes.Length - lastDataIndex;
							if (0 == restLength) stackedBytes = new byte[0];
							else stackedBytes = stackedBytes.SubArray(lastDataIndex, restLength);
						}
						
						
						// emit messages.
						if (0 < messageQueue.Count) {
							if (OnMessage != null) OnMessage(messageQueue);
							messageQueue.Clear();
						}
						return true;
					}
				}
			);
			
			/*
				main thread for websocket data receiving & sending.
			*/
			updater = Updater(
				throttle,
				"WebuSocket-main-thread",
				() => {
					switch (state) {
						case WSConnectionState.Opening: {
							var newSocket = WebSocketHandshake(url, additionalHeaderParams, OnError);
							
							if (newSocket != null) {
								this.socket = newSocket;
								
								state = WSConnectionState.Opened;
								
								if (OnConnected != null) OnConnected();
								break;
							}
							
							// handshake connection failed.
							// OnError handler is already fired.
							return false;
						}
						case WSConnectionState.Opened: {
							lock (socket) {
								while (0 < socket.Available) {
									var buff = new byte[socket.Available];
									socket.Receive(buff);
									lock (receivedDataList) receivedDataList.Add(buff);
								}
							}
							
							lock (stackedOrders) {
								while (0 < stackedOrders.Count) {
									var order = stackedOrders.Dequeue();
									ExecuteOrder(order);
								}
							}
							
							lock (stackedSendingDatas) {
								while (state == WSConnectionState.Opened && 0 < stackedSendingDatas.Count) {
									var data = stackedSendingDatas.Dequeue();
									var framedData = WebSocketByteGenerator.SendBinaryData(data);
									TrySend(framedData, OnError);
								}
							} 
							
							break;
						}
						case WSConnectionState.Closing: {
							lock (socket) {
								while (0 < socket.Available) {
									var buff = new byte[socket.Available];
									socket.Receive(buff);
									lock (receivedDataList) receivedDataList.Add(buff);
								}
							}
							
							lock (stackedOrders) {
								while (0 < stackedOrders.Count) {
									var order = stackedOrders.Dequeue();
									ExecuteOrder(order);
								}
							}
							break;
						}
						case WSConnectionState.Closed: {
							// break queue processor thread.
							receivedDataQueueConsumer.Abort();
							
							// break this thread.
							return false;
						}
					}
					return true;
				},
				OnClosed
			);
		}
		
		
		/*
			public methods.
		*/
		public WSConnectionState State () {
			switch (state){
				case WSConnectionState.Opening: return WSConnectionState.Opening;
				case WSConnectionState.Opened: return WSConnectionState.Opened;
				case WSConnectionState.Closing: return WSConnectionState.Closing;
				case WSConnectionState.Closed: return WSConnectionState.Closed;
				default: throw new Exception("unhandled state.");
			}
		}
		
		public bool IsConnected () {
			switch (state){
				case WSConnectionState.Opened: {
					if (socket != null) {
						if (socket.Connected) return true; 
					}
					return false;
				}
				default: return false;
			}
		}
		
		public void Ping (Action NewOnPong=null) {
			switch (state) {
				case WSConnectionState.Opened: {
					if (NewOnPong != null) this.OnPong = NewOnPong;
					StackOrder(WSOrder.Ping);
					break;
				}
				default: {
					Debug.LogError("current state is:" + state + ", ping operation request is ignored.");
					break;
				}
			}
		}
		
		public void Send (byte[] data) {
			switch (state) {
				case WSConnectionState.Opened: {
					StackData(data);
					break;
				}
				default: {
					Debug.LogError("current state is:" + state + ", send operation request is ignored.");
					break;
				}
			}
		}
		
		
		public void Close () {
			switch (state) {
				case WSConnectionState.Opened: {
					state = WSConnectionState.Closing;
					StackOrder(WSOrder.CloseGracefully);
					break;
				}
				default: {
					Debug.LogError("current state is:" + state + ", close operation request is ignored.");
					break;
				}
			}
		}
		
		/*
			forcely close socket on this time.
		*/
		public void CloseSync () {
			ForceClose();
		}
		
		
		
		/*
			private methods.
		*/
		
		private void StackData (byte[] data) {
			lock (stackedSendingDatas) stackedSendingDatas.Enqueue(data); 
		}
		
		private void StackOrder (WSOrder order) {
			lock (stackedOrders) stackedOrders.Enqueue(order);
		}
		
		
		private void ExecuteOrder (WSOrder order) {
			switch (order) {
				case WSOrder.Ping: {
					var data = WebSocketByteGenerator.Ping();
					TrySend(data);
					break;
				}
				case WSOrder.Pong: {
					var data = WebSocketByteGenerator.Pong();
					TrySend(data);
					break;
				}
				case WSOrder.CloseGracefully: {
					state = WSConnectionState.Closing;
					
					// this order is final one of this thread. ignore all other orders.
					stackedOrders.Clear();
					
					var data = WebSocketByteGenerator.CloseData();
					TrySend(data);
					ForceClose();
					break;
				}
				default: {
					Debug.LogError("unhandled order:" + order);
					break;
				}
			}
		}
		
		
		private void TrySend (byte[] data, Action<string, Exception> OnError=null) {
			try {
				socket.Send(data);
			} catch (SocketException e0) {
				switch (e0.SocketErrorCode) {
					case SocketError.WouldBlock: {
						if (OnError != null) OnError("failed to send data by SocketException:" + e0, e0);
						return;
					}
					default: {
						if (OnError != null) OnError("failed to send data by SocketException:" + e0 + ". attempt to close forcely.", e0);
						ForceClose(OnError);
						return;
					}
				}
			} catch (Exception e1) {
				if (OnError != null) OnError("failed to send data. " + e1 + ". attempt to close forcely.", e1);
				ForceClose(OnError);
			}
		}
		
		private void ForceClose (Action<string, Exception> OnError=null, Action<string> OnClosed=null) {
			if (state == WSConnectionState.Closed) {
				if (OnError != null) OnError("already closed.", null);
				return;
			}
			
			if (state == WSConnectionState.Opening) {
				Close();
				return;
			}
			
			if (socket == null) {
				if (OnError != null) OnError("not yet connected or already closed.", null);
				return;
			}
			
			if (!socket.Connected) {
				if (OnError != null) OnError("connection is already closed.", null);
				return;
			}
			
			lock (socket) {
				try {
					socket.Close();
				} catch (Exception e) {
					if (OnError != null) OnError("socket closing error:" + e, e);
				} finally {
					socket = null;
				}
				
				state = WSConnectionState.Closed;
			}
		}
		
		
		private Socket WebSocketHandshake (string urlSource, Dictionary<string, string> additionalHeaderParams, Action<string, Exception> OnError=null) {
			Debug.LogWarning("handshake timeoutの値どうしようかな、、そのままsocket使うからなんか影響しそう。");
			var timeout = 1000;
			
			
			var uri = new Uri(urlSource);
			
			var method = "GET";
			var host = uri.Host;
			var schm = uri.Scheme;
			var port = uri.Port;
			
			var base64Key = GeneratePrivateBase64Key();
			
			var requestHeaderParams = new Dictionary<string, string>{
				{"Host", (port == 80 && schm == "ws") || (port == 443 && schm == "wss") ? uri.DnsSafeHost : uri.Authority},
				{"Upgrade", "websocket"},
				{"Connection", "Upgrade"},
				{"Sec-WebSocket-Key", base64Key},
				{"Sec-WebSocket-Version", WEBSOCKET_VERSION}
			};
			
			if (additionalHeaderParams != null) { 
				foreach (var key in additionalHeaderParams.Keys) requestHeaderParams[key] = additionalHeaderParams[key];
			}
			
			/*
				construct request bytes data.
			*/
			var requestData = new StringBuilder();
			
			requestData.AppendFormat("{0} {1} HTTP/{2}{3}", method, uri, "1.1", CRLF);

			foreach (var key in requestHeaderParams.Keys) requestData.AppendFormat("{0}: {1}{2}", key, requestHeaderParams[key], CRLF);

			requestData.Append (CRLF);

			var entity = string.Empty;
			requestData.Append(entity);
			
			var requestDataBytes = Encoding.UTF8.GetBytes(requestData.ToString().ToCharArray());
			
			/*
				ready connection sockets.
			*/
			var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			sock.NoDelay = true;
			sock.SendTimeout = timeout;
			
			Action ForceCloseSock = () => {
				if (sock == null) return;
				
				if (!sock.Connected) {
					sock = null;
					return;
				}
			
				try {
					sock.Close();
				} catch {} finally {
					sock = null;
				}
			};
			
			try {
				sock.Connect(host, port);
			} catch (Exception e) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to connect to host:" + host + " error:" + e, e);
				return null;
			}
			
			if (!sock.Connected) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to connect.", null);
				return null;
			}
			
			try {
				var result = sock.Send(requestDataBytes);
				
				if (0 < result) {}// succeeded to send.
				else {
					ForceCloseSock();
					if (OnError != null) OnError("failed to send handshake request data, send size is 0.", null);
					
					return null;
				}
			} catch (Exception e) {
				
				ForceCloseSock();
				if (OnError != null) OnError("failed to send handshake request data. error:" + e, e);
				
				return null;
			}
			
			
			
			/*
				read connection response from socket.
			*/
			var responseHeaderDict = new Dictionary<string, string>();
			{
				/*
					protocol should be switched.
				*/
				var protocolResponse = ReadLineBytes(sock);
				if (!string.IsNullOrEmpty(protocolResponse.error)) {
					ForceCloseSock();
					if (OnError != null) OnError("failed to receive response.", null);
					return null;
				}
				
				if (Encoding.UTF8.GetString(protocolResponse.data).ToLower() != "HTTP/1.1 101 Switching Protocols".ToLower()) {
					ForceCloseSock();
					if (OnError != null) OnError("failed to switch protocol.", null);
					return null;
				}
				
				if (sock.Available == 0) {
					ForceCloseSock();
					if (OnError != null) OnError("failed to receive rest of response header.", null);
					return null;
				}
				
				/*
					rest data exists and can be received.
				*/
				while (0 < sock.Available) {
					var responseHeaderLineBytes = ReadLineBytes(sock);
					if (!string.IsNullOrEmpty(responseHeaderLineBytes.error)) {
						ForceCloseSock();
						if (OnError != null) OnError("responseHeaderLineBytes.error:" + responseHeaderLineBytes.error, null);
						return null;
					}
					
					var responseHeaderLine = Encoding.UTF8.GetString(responseHeaderLineBytes.data);
					
					if (!responseHeaderLine.Contains(":")) continue;
					
					var splittedKeyValue = responseHeaderLine.Split(':');
					
					var key = splittedKeyValue[0].ToLower();
					var val = splittedKeyValue[1];
					
					responseHeaderDict[key] = val;
				}
			}
				
			// validate.
			if (!responseHeaderDict.ContainsKey("Server".ToLower())) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to receive 'Server' key.", null);
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Date".ToLower())) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to receive 'Date' key.", null);
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Connection".ToLower())) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to receive 'Connection' key.", null);
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Upgrade".ToLower())) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to receive 'Upgrade' key.", null);
				return null;
			}
			
			if (!responseHeaderDict.ContainsKey("Sec-WebSocket-Accept".ToLower())) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to receive 'Sec-WebSocket-Accept' key.", null);
				return null;
			}
			var serverAcceptedWebSocketKey = responseHeaderDict["Sec-WebSocket-Accept".ToLower()];
			
			if (!sock.Connected) {
				ForceCloseSock();
				if (OnError != null) OnError("failed to check connected after validate.", null);
				return null;
			}
			
			return sock;
		}
		
		private byte[] httpResponseReadBuf = new byte[HTTP_HEADER_LINE_BUF_SIZE];

		public ResponseHeaderLineDataAndError ReadLineBytes (Socket sock) {
			byte[] b = new byte[1];
			
			var readyReadLength = sock.Available;
			if (httpResponseReadBuf.Length < readyReadLength) {
				new ResponseHeaderLineDataAndError(new byte[0], "too long data for read as line found");
			} 
			
			/*
				there are not too long data.
				"readyReadLength <= HTTP_HEADER_LINE_BUF_SIZE".
				
				but it is not surpported that this socket contains data which containes "\n".
				cut out when buffering reached to full.
			*/
			int i = 0;
			while (true) {
				sock.Receive(b);
				
				if (b[0] == '\r') continue;
				if (b[0] == '\n') break;
				
				httpResponseReadBuf[i] = b[0];
				i++;
				
				if (i == readyReadLength) {
					Debug.LogError("no \n appears. cut out.");
					break;
				}
			}
			
			var retByte = new byte[i];
			Array.Copy(httpResponseReadBuf, 0, retByte, 0, i);

			return new ResponseHeaderLineDataAndError(retByte);
		}
		
		public struct ResponseHeaderLineDataAndError {
			public readonly byte[] data;
			public readonly string error;
			public ResponseHeaderLineDataAndError (byte[] data, string error=null) {
				this.data = data;
				this.error = error;
			}
		}
		
		private static string GeneratePrivateBase64Key () {
			var src = new byte[16];
			randomGen.GetBytes(src);
			return Convert.ToBase64String(src);
		}
		
		public static byte[] NewMaskKey () {
			var maskingKeyBytes = new byte[4];
			randomGen.GetBytes(maskingKeyBytes);
			return maskingKeyBytes;
		}
		
		/**
			2 loop type.
			switched by throttle.
		*/
		private Thread Updater (int throttle, string loopId, Func<bool> OnUpdate, Action<string> OnClosed=null) {
			Action loopMethod = null;
			
			// limited frame update.
			if (0 < throttle) {
				var framePerSecond =throttle;
				var mainThreadInterval = 1000f / framePerSecond;
				
				loopMethod = () => {
					try {
						double nextFrame = (double)System.Environment.TickCount;
						
						var before = 0.0;
						var tickCount = (double)System.Environment.TickCount;
						
						while (true) {
							tickCount = System.Environment.TickCount * 1.0;
							if (nextFrame - tickCount > 1) {
								Thread.Sleep((int)(nextFrame - tickCount)/2);
								/*
									waitを半分くらいにすると特定フレームで安定する。よくない。
								*/
								continue;
							}
							
							if (tickCount >= nextFrame + mainThreadInterval) {
								nextFrame += mainThreadInterval;
								continue;
							}
							
							// run action for update.
							var continuation = OnUpdate();
							if (!continuation) break;
							
							nextFrame += mainThreadInterval;
							before = tickCount;
						}
						
						if (OnClosed != null) OnClosed("WebuSocket:" + webSocketConnectionId + " loopId:" + loopId + " is finished gracefully.");
					} catch (Exception e) {
						if (OnClosed != null) OnClosed("WebuSocket:" + webSocketConnectionId + " loopId:" + loopId + " finished with error:" + e);
					}
				};
			} else {
				loopMethod = () => {
					try {
						while (true) {
							// run action for update.
							var continuation = OnUpdate();
							if (!continuation) break;
							
							Thread.Sleep(1);
						}
						
						if (OnClosed != null) OnClosed("WebuSocket:" + webSocketConnectionId + " loopId:" + loopId + " is finished gracefully.");
					} catch (Exception e) {
						if (OnClosed != null) OnClosed("WebuSocket:" + webSocketConnectionId + " loopId:" + loopId + " finished with error:" + e);
					}
				};
			}
			
			var thread = new Thread(new ThreadStart(loopMethod));
			thread.Start();
			return thread;
		}
	}
}
