using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace DisquuunCore {
    public enum DisqueCommand {		
		ADDJOB,// queue_name job <ms-timeout> [REPLICATE <count>] [DELAY <sec>] [RETRY <sec>] [TTL <sec>] [MAXLEN <count>] [ASYNC]
		GETJOB,// [NOHANG] [TIMEOUT <ms-timeout>] [COUNT <count>] [WITHCOUNTERS] FROM queue1 queue2 ... queueN
		ACKJOB,// jobid1 jobid2 ... jobidN
		FASTACK,// jobid1 jobid2 ... jobidN
		WORKING,// jobid
		NACK,// <job-id> ... <job-id>
		INFO,
		HELLO,
		QLEN,// <queue-name>
		QSTAT,// <queue-name>
		QPEEK,// <queue-name> <count>
		ENQUEUE,// <job-id> ... <job-id>
		DEQUEUE,// <job-id> ... <job-id>
		DELJOB,// <job-id> ... <job-id>
		SHOW,// <job-id>
		QSCAN,// [COUNT <count>] [BUSYLOOP] [MINLEN <len>] [MAXLEN <len>] [IMPORTRATE <rate>]
		JSCAN,// [<cursor>] [COUNT <count>] [BUSYLOOP] [QUEUE <queue>] [STATE <state1> STATE <state2> ... STATE <stateN>] [REPLY all|id]
		PAUSE,// <queue-name> option1 [option2 ... optionN]
	}
	
	/**
		data structure for vector.
	*/
	public struct ByteDatas {
		public byte[][] bytesArray;
		
		public ByteDatas (params byte[][] bytesArray) {
			this.bytesArray = bytesArray;
		}
	}
	
    public class Disquuun {
		public readonly string connectionId;
		
		public readonly long BufferSize;
		public readonly IPEndPoint endPoint;
		
		public ConnectionState connectionState;
		
		
		private readonly Action<Exception> ConnectionFailed;
		
		private List<SocketObject> socketPool;
		
		public enum ConnectionState {
			OPENED,
			ALLCLOSING,
			ALLCLOSED
		}
		
		
		public Disquuun (
			string host,
			int port,
			long bufferSize,
			int maxConnectionCount,
			Action<Exception> ConnectionFailed=null
		) {
			this.connectionId = Guid.NewGuid().ToString();
			
			this.BufferSize = bufferSize;
			this.endPoint = new IPEndPoint(IPAddress.Parse(host), port);
			
			this.connectionState = ConnectionState.ALLCLOSED;
			
			
			/*
				set handlers for connection error.
				other runtime errors will emit in API handler.
			*/
			this.ConnectionFailed = ConnectionFailed;
			
			socketPool = new List<SocketObject>();
			for (var i = 0; i < maxConnectionCount; i++) {
				var socketObj = new SocketObject(endPoint, bufferSize, OnSocketConnectionFailed);
				socketPool.Add(socketObj);
			}
		}
		
		
		private void OnSocketConnectionFailed (SocketObject source, Exception e) {
			lock (socketPool) {
				socketPool.Remove(source);
				if (ConnectionFailed != null) ConnectionFailed(e); 
			}
			
			State();
		}
		
		public ConnectionState State () {
			lock (socketPool) {
				var connectionCount = socketPool
					.Where(
						s => 
							s.State() == SocketObject.SocketState.OPENED || 
							s.State() == SocketObject.SocketState.OPENING
					).ToArray().Length;
				
				switch (connectionCount) {
					case 0: {
						connectionState = ConnectionState.ALLCLOSED;
						break;
					}
				}
			}
			
			return connectionState;
		}
		
		
		public void Disconnect () {
			connectionState = ConnectionState.ALLCLOSING;
			foreach (var socket in socketPool) socket.Disconnect();
		}
		
		
		/*
			API gateway
		*/
		public byte[] AddJob (string queueName, byte[] data, int timeout=0, params object[] args) {
			return DisquuunAPI.AddJob(queueName, data, timeout, args);
		}
		
		public byte[] GetJob (string[] queueIds, params object[] args) {
			return DisquuunAPI.GetJob(queueIds, args);
		}
		
		public byte[] AckJob (string[] jobIds) {
			return DisquuunAPI.AckJob(jobIds);
		}

		public byte[] FastAck (string[] jobIds) {
			return DisquuunAPI.FastAck(jobIds);
		}

		public byte[] Working (string jobId) {
			return DisquuunAPI.Working(jobId);
		}

		public byte[] Nack (string[] jobIds) {
			return DisquuunAPI.Nack(jobIds);
		}
		
		public byte[] Info () {
			return DisquuunAPI.Info();
		}
		
		public byte[] Hello () {
			return DisquuunAPI.Hello();
		}
		
		public byte[] Qlen (string queueId) {
			return DisquuunAPI.Qlen(queueId);
		}
		
		/*
			QSTAT,// <queue-name>
			QPEEK,// <queue-name> <count>
			ENQUEUE,// <job-id> ... <job-id>
			DEQUEUE,// <job-id> ... <job-id>
			DELJOB,// <job-id> ... <job-id>
			SHOW,// <job-id>
			QSCAN,// [COUNT <count>] [BUSYLOOP] [MINLEN <len>] [MAXLEN <len>] [IMPORTRATE <rate>]
			JSCAN,// [<cursor>] [COUNT <count>] [BUSYLOOP] [QUEUE <queue>] [STATE <state1> STATE <state2> ... STATE <stateN>] [REPLY all|id]
			PAUSE,// <queue-name> option1 [option2 ... optionN]
		*/
		
		
		
		
		public static void Log (string message) {
			// TestLogger.Log(message);
		}
		
		
		// socketPoolを用意して、そこにいろいろやらせるスタイル。
		
		
		public class SocketObject {
			private Action<SocketObject, Exception> ConnectionFailed;
			
			/*
				tokenとかもなんかsocketPoolに分離する必要がある。
				DisquuunSocketクラス作ろう。
				
				エラーは、接続状態の切断 = 死を取得できるようにして、
			*/
			private SocketToken socketToken;
			
			public SocketState State () {
				return socketToken.socketState;
			}
			
			public enum SocketState {
				OPENING,
				OPENED,
				BUSY,
				CLOSING,
				CLOSED
			}
			
			public struct SocketToken {
				public SocketState socketState;
				
				public readonly Socket socket;
				
				public readonly SocketAsyncEventArgs connectArgs;
				public readonly SocketAsyncEventArgs sendArgs;
				public readonly SocketAsyncEventArgs receiveArgs;
				
				public Queue<DisqueCommand> stack;
				
				public SocketToken (Socket socket, SocketAsyncEventArgs connectArgs, SocketAsyncEventArgs sendArgs, SocketAsyncEventArgs receiveArgs) {
					this.socketState = SocketState.OPENING;
					this.socket = socket;
					
					this.connectArgs = connectArgs;
					this.sendArgs = sendArgs;
					this.receiveArgs = receiveArgs;
					
					this.stack = new Queue<DisqueCommand>();
					
					this.connectArgs.UserToken = this;
					this.sendArgs.UserToken = this;
					this.receiveArgs.UserToken = this;
				}
			}
			
			public SocketObject (IPEndPoint endPoint, long bufferSize, Action<SocketObject, Exception> ConnectionFailed) {
				this.ConnectionFailed = ConnectionFailed;
				
				var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				
				var connectArgs = new SocketAsyncEventArgs();
				connectArgs.AcceptSocket = clientSocket;
				connectArgs.RemoteEndPoint = endPoint;
				connectArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnConnected);
				
				var sendArgs = new SocketAsyncEventArgs();
				sendArgs.AcceptSocket = clientSocket;
				sendArgs.RemoteEndPoint = endPoint;
				sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSend);
				
				var receiveArgs = new SocketAsyncEventArgs();
				byte[] receiveBuffer = new byte[bufferSize];
				receiveArgs.SetBuffer(receiveBuffer, 0, receiveBuffer.Length);
				receiveArgs.AcceptSocket = clientSocket;
				receiveArgs.RemoteEndPoint = endPoint;
				receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnReceived);
							
				socketToken = new SocketToken(clientSocket, connectArgs, sendArgs, receiveArgs);
				
				// start connect.
				if (!clientSocket.ConnectAsync(socketToken.connectArgs)) OnConnected(clientSocket, connectArgs);
			}
			
			
			/*
				handlers
				
				SyncとAsyncとLoopに分ける必要がある。先にテスト書きたいな。
				
				んーーー実際にはどんな実装になるんだろう。データの放り込み方が異なるだけだ。
			*/
			public ByteDatas[] Sync (byte[] data) {
				Log("同期なんで、送り込んでデータが帰ってくるまで待つ。うむうむ。");
				var data2 = new ByteDatas[10];
				socketToken.socketState = SocketState.OPENED;
				return data2;
			}
			
			public void Async (byte[] data) {
				Log("このソケットへとAsyncでデータを送り込む。前提として、BUSYでない必要があるが、そのへんはもう果たしておけるはず。");
			}
			
			
			
			
			private void OnConnected (object unused, SocketAsyncEventArgs args) {
				var token = (SocketToken)args.UserToken;
				switch (token.socketState) {
					
					case SocketState.OPENING: {
						if (args.SocketError != SocketError.Success) {
							token.socketState = SocketState.CLOSED;
							var error = new Exception("connect error:" + args.SocketError.ToString());
							
							Disquuun.Log("まだいろいろハンドルしてない。このソケットでの単体の失敗なんで、Disquuun自体の失敗なのか。ハンドラいるな、、AddSlotとかで露出させたほうが楽か。");
							ConnectionFailed(this, error);
							return;
						}
						
						token.socketState = SocketState.OPENED;
						
						// ready receive data.
						token.socket.ReceiveAsync(token.receiveArgs);
						
						Disquuun.Log("まだConnectedハンドルしてない");
						// if (Connected != null) Connected(connectionId); 
						return;
					}
					default: {
						throw new Exception("unknown connect error:" + token.socketState);
					}
				}
			}
			
			private void OnClosed (object unused, SocketAsyncEventArgs args) {
				var token = (SocketToken)args.UserToken;
				switch (token.socketState) {
					case SocketState.CLOSED: {
						// do nothing.
						break;
					}
					default: {
						token.socketState = SocketState.CLOSED;
						Disquuun.Log("まだCloseハンドルしてない");
						// if (Closed != null) Closed(this.connectionId);
						break;
					}
				}
			}
			
			private void OnSend (object unused, SocketAsyncEventArgs args) {
				var socketError = args.SocketError;
				switch (socketError) {
					case SocketError.Success: {
						// do nothing.
						break;
					}
					default: {
						Disquuun.Log("まだエラーハンドルしてない");
						// if (Error != null) {
						// 	var error = new Exception("send error:" + socketError.ToString());
						// 	Error(error);
						// }
						break;
					}
				}
			}
			
			/*
				ReceiveはSocket単位になるはず。Syncかそれ以外で挙動が異なり、Syncの場合はそもそもここに来ない。
			*/
			private void OnReceived (object unused, SocketAsyncEventArgs args) {
			
				var token = (SocketToken)args.UserToken;
				if (args.SocketError != SocketError.Success) { 
					switch (token.socketState) {
						case SocketState.CLOSING:
						case SocketState.CLOSED: {
							// already closing, ignore.
							return;
						}
						default: {
							// show error, then close or continue receiving.
							
							Disquuun.Log("まだエラーハンドルしてない");
							// if (Error != null) {
							// 	var error = new Exception("receive error:" + args.SocketError.ToString() + " size:" + args.BytesTransferred);
							// 	Error(error);
							// }
							
							// connection is already closed.
							if (!IsSocketConnected(token.socket)) {
								Disconnect();
								return;
							}
							
							// continue receiving data. go to below.
							break;
						}
					}
				}
				
				if (0 < args.BytesTransferred) {
					if (0 < token.stack.Count) { 
						var dataSource = args.Buffer;
						var bytesAmount = args.BytesTransferred;
						
						var rest = args.AcceptSocket.Available;
						if (0 < rest) {
							var restBuffer = new byte[rest];
							var additionalReadResult = token.socket.Receive(restBuffer, SocketFlags.None);
							
							var baseLength = dataSource.Length;
							Array.Resize(ref dataSource, baseLength + rest);
							
							for (var i = 0; i < rest; i++) dataSource[baseLength + i] = restBuffer[i];
							bytesAmount = dataSource.Length;
						}
						
						Disquuun.Log("まだフィルタ通してない。");
						// DisquuunAPI.Evaluate(token.stack, bytesAmount, dataSource, Received, Failed);
					}
				}
				
				// continue to receive.
				if (!token.socket.ReceiveAsync(args)) OnReceived(null, args);
			}
			
			
			public void Disconnect () {
				switch (socketToken.socketState) {
					case SocketState.CLOSING:
					case SocketState.CLOSED: {
						// do nothing
						break;
					}
					default: {
						socketToken.socketState = SocketState.CLOSING;
						
						var closeEventArgs = new SocketAsyncEventArgs();
						closeEventArgs.UserToken = socketToken;
						closeEventArgs.AcceptSocket = socketToken.socket;
						closeEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnClosed);
						
						if (!socketToken.socket.DisconnectAsync(closeEventArgs)) OnClosed(socketToken.socket, closeEventArgs);
						break;
					}
				}
			}
			
			
			/*
				utils
			*/
			
			private static bool IsSocketConnected (Socket s) {
				bool part1 = s.Poll(1000, SelectMode.SelectRead);
				bool part2 = (s.Available == 0);
				
				if (part1 && part2) return false;
				
				return true;
			}
		}
	}
}