
using System;
using System.Globalization;
using System.Text;

namespace DisquuunCore {
	
	public static class DisquuunAPI {
		/*
			disque protocol symbols
		*/
		public enum CommandString {
			Error = '-',
			Status = '+',
			Bulk = '$',
			MultiBulk = '*',
			Int = ':'
		}
		
		/*
			chars
		*/
		public const char CharError = (char)CommandString.Error;
		public const char CharStatus = (char)CommandString.Status;
		public const char CharBulk = (char)CommandString.Bulk;
		public const char CharMultiBulk = (char)CommandString.MultiBulk;
		public const char CharInt = (char)CommandString.Int;
		public const string CharEOL = "\r\n";
		
		public const string DISQUE_GETJOB_KEYWORD_FROM = "FROM";
		
		
		/*
			Disque APIs.
			一時的にstaticにしておくが、disquuunインスタンスから叩けるようにしておくのが理想。
			
			connectedとかconnect failedとかをどう隠蔽するのかっていうのは考えものだな、、
		*/
		public static byte[] AddJob (string queueName, byte[] data, int timeout=0, params object[] args) {
			// ADDJOB queue_name job <ms-timeout> 
			// [REPLICATE <count>] [DELAY <sec>] [RETRY <sec>] [TTL <sec>] [MAXLEN <count>] [ASYNC]
			
			// byteをそのまま送りたいんだが、っていうやつ。byteArrayをそのままではうまく変形できない。
			// あと、いろいろ配列で渡さないといけないんだけど、それも辛い。
			// この段階で
			var dataStr = Encoding.UTF8.GetString(data);
			// var newArgs = new object[1 + args.Length];
			// newArgs[0] = timeout;
			// for (var i = 1; i < newArgs.Length; i++) newArgs[i] = args[i-1];
			return  ToBytes(DisqueCommand.ADDJOB, queueName, dataStr, timeout);
		}
		
		public static byte[] GetJob (string[] queueIds, params object[] args) {
			// [NOHANG] [TIMEOUT <ms-timeout>] [COUNT <count>] [WITHCOUNTERS] 
			// FROM queue1 queue2 ... queueN
			var parameters = new object[args.Length + 1 + queueIds.Length];
			for (var i = 0; i < parameters.Length; i++) {
				if (i < args.Length) {
					parameters[i] = args[i];
					continue;
				}
				if (i == args.Length) {
					parameters[i] = DISQUE_GETJOB_KEYWORD_FROM;
					continue;
				}
				parameters[i] = queueIds[i - (args.Length + 1)];
			}
			// foreach (var i in parameters) {
			// 	Log("i:" + i);
			// }
			return ToBytes(DisqueCommand.GETJOB, parameters);
		}
		
		public static byte[] AckJob (string[] jobIds) {
			// jobid1 jobid2 ... jobidN
			return ToBytes(DisqueCommand.ACKJOB, jobIds);
		}

		public static byte[] FastAck (string[] jobIds) {
			// jobid1 jobid2 ... jobidN
			return ToBytes(DisqueCommand.FASTACK, jobIds);
		}

		public static byte[] Working (string jobId) {
			// jobid
			return ToBytes(DisqueCommand.WORKING, jobId);
		}

		public static byte[] Nack (string[] jobIds) {
			// <job-id> ... <job-id>
			return ToBytes(DisqueCommand.NACK, jobIds);
		}
		
		public static byte[] Info () {
			return ToBytes(DisqueCommand.INFO);
		}
		
		public static byte[] Hello () {
			return ToBytes(DisqueCommand.HELLO);
		}
		
		public static byte[] Qlen (string queueId) {
			// QLEN,// <queue-name>
			return ToBytes(DisqueCommand.QLEN, queueId);
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
		
		/*
			byte comberter
		*/
		private static byte[] ToBytes (DisqueCommand commandEnum, params object[] args) {
			int length = 1 + args.Length;
			
			var command = commandEnum.ToString();
			string strCommand;
			
			// 自前のbyte memory streamを使うかな。StringBuilder 重たいんで使いたくない。あとでベンチ。
			{
				StringBuilder sb = new StringBuilder();
				sb.Append(CharMultiBulk).Append(length).Append(CharEOL);
				
				sb.Append(CharBulk).Append(Encoding.UTF8.GetByteCount(command)).Append(CharEOL).Append(command).Append(CharEOL);
				
				foreach (var arg in args) {
					var str = String.Format(CultureInfo.InvariantCulture, "{0}", arg);
					sb.Append(CharBulk)
						.Append(Encoding.UTF8.GetByteCount(str))
						.Append(CharEOL)
						.Append(str)
						.Append(CharEOL);
				}
				strCommand = sb.ToString();
			}
			// Log("strCommand:" + strCommand);
			
			// 結局byteに変換してるんだよな~ なので、自前のやつに入れ替える。
			
			byte[] bytes = Encoding.UTF8.GetBytes(strCommand.ToCharArray());
			
			return bytes;
			// socketToken.stack.Enqueue(commandEnum);
			// socketToken.sendArgs.SetBuffer(bytes, 0, bytes.Length);
			
			// if (!socketToken.socket.SendAsync(socketToken.sendArgs)) OnSend(socketToken.socket, socketToken.sendArgs);
		}
	}
}