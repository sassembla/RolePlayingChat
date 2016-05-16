using XrossPeerUtility;

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using DisquuunCore;
using DisquuunCore.Deserialize;

public class DisqueConnectionController {
	private ServerContext context;
	private Disquuun disquuun;
	
	public DisqueConnectionController (string contextQueueIdentity) {
		disquuun = new Disquuun(
			"127.0.0.1", 7711, 1024 * 100, 3, 
			conId => {
				disquuun.GetJob(new string[]{contextQueueIdentity}, "count", 1000).Loop(
					(command, data) => {
						var jobs = DisquuunDeserializer.GetJob(data).Select(jobData => jobData.jobData).ToList();
						InputDatasToContext(jobs);
						return true;
					}
				);
			}
		);
	}
	
	public void Disconnect () {
		if (disquuun != null) disquuun.Disconnect(true);
	}

	public void SetContext (ServerContext context) {
		this.context = context;
		context.Setup(Publish);
	}
	
	public void Publish (string targetConnectionId, byte[] data) {
		if (disquuun != null && disquuun.connectionState == Disquuun.ConnectionState.OPENED) {
		} else {
			Disquuun.Log("not yet publicable.");
			return;
		}
		
		disquuun.AddJob(targetConnectionId, data).Async(
			(command, result) => {
				
			}
		);
	}
	
	// こっからフィルタ。
	/*
		フィルタは、staticでいいんで、どっかにコピーして成立させよう。
	*/


	// header of data.
	public const char HEADER_STRING	= 's';
	public const char HEADER_BINARY	= 'b';
	public const char HEADER_CONTROL	= 'c';

	// webSocket server state for each connection. syncronized to nginx-lua client.lua code.
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
			if (len < 1/*s or b or c*/ + 1/*state param*/ + CONNECTION_ID_LEN/*connectionId*/) {
				var invalidMessage = Encoding.ASCII.GetString(dataArray);
				XrossPeer.Log("illigal format invalidMessage1:" + invalidMessage);
				continue;
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
					continue;
				}
			}
			
			var state = (char)dataArray[1];

			// dataArray[2-38] is connectionId, length = definitely CONNECTION_ID_LEN.
			var connectionId = Encoding.ASCII.GetString(dataArray, 2, CONNECTION_ID_LEN);
			
			switch (state) {
				case STATE_CONNECT: {
					XrossPeer.Log("STATE_CONNECT");
					if (2 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (2 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
						context.OnConnected(connectionId, data);	
					}
					break;
				}

				case STATE_STRING_MESSAGE: {
					// if (2 + CONNECTION_ID_LEN < len) {
					// 	var data = Encoding.UTF8.GetString(dataArray, 2 + CONNECTION_ID_LEN, len - (2 + CONNECTION_ID_LEN));
					// 	context.OnMessage(connectionId, data);
					// }
					break;
				}

				case STATE_BINARY_MESSAGE: {
					if (2 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (2 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
						context.OnMessage(connectionId, data);
					}
					break;
				}

				case STATE_DISCONNECT_INTENT: {
					if (2 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (2 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
						XrossPeer.Log("client closed");
						context.OnDisconnected(connectionId, data, "intentional disconnect.");
					}
					break;
				}

				case STATE_DISCONNECT_ACCIDT: {
					if (2 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (2 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
						
						context.OnDisconnected(connectionId, data, "accidential disconnect.");
					}
					break;
				}
				case STATE_DISCONNECT_DISQUE_ACKFAILED: {
					if (2 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (2 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
						
						context.OnDisconnected(connectionId, data, "accidential disconnect.");
					}
					break;
				}
				case STATE_DISCONNECT_DISQUE_ACCIDT_SENDFAILED: {
					if (2 + CONNECTION_ID_LEN < len) {
						var dataLen = len - (2 + CONNECTION_ID_LEN);
						var data = new byte[dataLen];
						Buffer.BlockCopy(dataArray, (2 + CONNECTION_ID_LEN), data, 0, dataLen);
						
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



public class DisqueConnectionSocket {
	private readonly string info;

	const int bufferSize = 1024;// bytes, 1K
	private byte[] buf = new byte[bufferSize];

	private Socket sock;

	private static byte[] bytes_aster;
	private static byte[] bytes_r_n;
	private static byte[] bytes_dollar;
	
	/**
		コンストラクタ
	*/
	public DisqueConnectionSocket (string info, string host, int port, int timeout) {
		
		this.info = info;

		this.sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		this.sock.NoDelay = true;
		this.sock.SendTimeout = timeout;

		bytes_aster = Encoding.UTF8.GetBytes("*");
		bytes_r_n = Encoding.UTF8.GetBytes("\r\n");
		bytes_dollar = Encoding.UTF8.GetBytes("$");

		try {
			var dns = new IPEndPoint(IPAddress.Parse(host), port);
			this.sock.Connect(dns);
		} catch (Exception e) {
			XrossPeer.Log("ERROR: DisqueConnectionController: failed to connect to disque @:" + host + ":" + port);
			XrossPeer.Log("ERROR: reason:" + e);
		}
		
		if (!this.sock.Connected) {
			// failed to create connection. 
			this.sock.Close();
			this.sock = null;
			throw new Exception("failed to connect to disque @:" + host + ":" + port);
		}
	}

	public void Close () {
		this.sock.Close();
		this.sock = null;
	}

	/**
		send byte data to server with disque format. sync.
	*/
	public int SendBytesSync (string cmd, string queueId, byte[] data, params string[] options) {
		if (sock == null) return 0;

		var byteBuffer = new MemoryStream();

		var contentCount = 1;// count of command.
		
		if (!string.IsNullOrEmpty(queueId)) {
			contentCount++;
		}

		if (0 < data.Length) {
			contentCount++;
		}

		if (0 < options.Length) {
			contentCount = contentCount + options.Length;
		}

		// "*" + contentCount.ToString() + "\r\n"
		{
			var contentCountBytes = Encoding.UTF8.GetBytes(contentCount.ToString());
			
			byteBuffer.Write(bytes_aster, 0, bytes_aster.Length);
			byteBuffer.Write(contentCountBytes, 0, contentCountBytes.Length);
			byteBuffer.Write(bytes_r_n, 0, bytes_r_n.Length);
		}

		// "$" + cmd.Length + "\r\n" + cmd + "\r\n"
		{
			var commandBytes = Encoding.UTF8.GetBytes(cmd);
			var commandCountBytes = Encoding.UTF8.GetBytes(cmd.Length.ToString());
		
			byteBuffer.Write(bytes_dollar, 0, bytes_dollar.Length);
			byteBuffer.Write(commandCountBytes, 0, commandCountBytes.Length);
			byteBuffer.Write(bytes_r_n, 0, bytes_r_n.Length);
			byteBuffer.Write(commandBytes, 0, commandBytes.Length);
			byteBuffer.Write(bytes_r_n, 0, bytes_r_n.Length);
		}

		// "$" + queueId.Length + "\r\n" + queueId + "\r\n"
		if (!string.IsNullOrEmpty(queueId)) {
			var queueIdBytes = Encoding.UTF8.GetBytes(queueId);
			var queueIdCountBytes = Encoding.UTF8.GetBytes(queueId.Length.ToString());
			
			byteBuffer.Write(bytes_dollar, 0, bytes_dollar.Length);
			byteBuffer.Write(queueIdCountBytes, 0, queueIdCountBytes.Length);
			byteBuffer.Write(bytes_r_n, 0, bytes_r_n.Length);
			byteBuffer.Write(queueIdBytes, 0, queueIdBytes.Length);
			byteBuffer.Write(bytes_r_n, 0, bytes_r_n.Length);
		}

		// "$" + data.Length + "\r\n" + data + "\r\n"
		if (0 < data.Length) {
			var dataCountBytes = Encoding.UTF8.GetBytes(data.Length.ToString());
			
			byteBuffer.Write(bytes_dollar, 0, bytes_dollar.Length);
			byteBuffer.Write(dataCountBytes, 0, dataCountBytes.Length);
			byteBuffer.Write(bytes_r_n, 0, bytes_r_n.Length);
			byteBuffer.Write(data, 0, data.Length);
			byteBuffer.Write(bytes_r_n, 0, bytes_r_n.Length);
		}

		// "$" + option.Length + "\r\n" + option + "\r\n"
		if (0 < options.Length) {
			foreach (var option in options) {
				var optionBytes = Encoding.UTF8.GetBytes(option.ToString());
				var optionCountBytes = Encoding.UTF8.GetBytes(option.Length.ToString());
			
				byteBuffer.Write(bytes_dollar, 0, bytes_dollar.Length);
				byteBuffer.Write(optionCountBytes, 0, optionCountBytes.Length);
				byteBuffer.Write(bytes_r_n, 0, bytes_r_n.Length);
				byteBuffer.Write(optionBytes, 0, optionBytes.Length);
				byteBuffer.Write(bytes_r_n, 0, bytes_r_n.Length);
			}	
		}

		try {
			sock.Send(byteBuffer.ToArray());

			return sock.Available;
		} catch (Exception e) {
			XrossPeer.LogError("socket:" + info + " error:" + e);
			
			sock.Close();
			sock = null;
		}

		return 0;
	}

	
	public byte ReadFirstByte () {
		byte[] b = new byte[1];
		sock.Receive(b);
		return b[0];
	}

	public byte[] ReadLineBytes () {
		byte[] b = new byte[1];
		
		int limit = sock.Available;

		int i = 0;
		while (true) {
			sock.Receive(b);
			if (b[0] == '\r') continue;
			if (b[0] == '\n') break;

			buf[i] = b[0];
			
			if (i == limit) {
				XrossPeer.Log("limit by Available.");
				break;
			}

			if (i == buf.Length) {
				XrossPeer.Log("too large line.");
				break;
			}

			i++;
		}
		var retByte = new byte[i];
		Array.Copy(buf, 0, retByte, 0, i);

		return retByte;
	}

	public byte[] ReadBytes (int length) {
		byte[] retByte = new byte[length];
		
		int limit = sock.Available;
		if (limit < length + 2) {
			throw new Exception("failed to receive request length. too long for receive.");
		}

		sock.Receive(retByte);
		limit = sock.Available;

		byte[] b = new byte[1];
		sock.Receive(b);
		sock.Receive(b);

		return retByte;
	}
}
