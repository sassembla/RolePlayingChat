using System;
using System.Collections.Generic;
using System.Text;

namespace DisquuunCoreOld.Deserialize {
	
	public static class DisquuunDeserializer {
		
		public static string AddJob (DisquuunOld.ByteDatas[] data) {
			var idStrBytes = data[0].bytesArray[0];
			return Encoding.UTF8.GetString(idStrBytes);
		}
		
		public struct JobData {
			public readonly string jobId;
			public readonly byte[] jobData;
			
			public readonly int nackCount;
			public readonly int additionalDeliveriesCount;
			
			public JobData (DisquuunOld.ByteDatas dataSourceBytes) {
				this.jobId = Encoding.UTF8.GetString(dataSourceBytes.bytesArray[0]);
				this.jobData = dataSourceBytes.bytesArray[1];
				if (dataSourceBytes.bytesArray.Length < 3) {
					nackCount = -1;
					additionalDeliveriesCount = -1;
				} else {// with "withcounters" option
					nackCount = Convert.ToInt32(Encoding.UTF8.GetString(dataSourceBytes.bytesArray[2]));
					additionalDeliveriesCount = Convert.ToInt32(Encoding.UTF8.GetString(dataSourceBytes.bytesArray[3]));
				}
			}
		}
		
		public static JobData[] GetJob (DisquuunOld.ByteDatas[] data) {
			var jobDatas = new JobData[data.Length];
			for (var i = 0; i < data.Length; i++) {
				var jobDataSource = data[i];
				jobDatas[i] = new JobData(jobDataSource);
			}
			return jobDatas;
		}
	
		public static int DeserializeInt (DisquuunOld.ByteDatas[] data) {
			var valStr = Encoding.UTF8.GetString(data[0].bytesArray[0]);
			return Convert.ToInt32(valStr);
		}
		public static int AckJob (DisquuunOld.ByteDatas[] data) {
			return DeserializeInt(data);
		}
		public static int FastAck (DisquuunOld.ByteDatas[] data) {
			return DeserializeInt(data);
		}
		public static int Working (DisquuunOld.ByteDatas[] data) {
			return DeserializeInt(data);
		}
		
		public static int Nack (DisquuunOld.ByteDatas[] data) {
			return DeserializeInt(data);
		}
		
		public static string Info (DisquuunOld.ByteDatas[] data) {
			return Encoding.UTF8.GetString(data[0].bytesArray[0]);
		}
		
		public struct HelloData {
			public readonly string version;
			public readonly string sourceNodeId;
			public readonly NodeData[] nodeDatas;
			public HelloData (string version, string sourceNodeId, NodeData[] nodeDatas) {
				this.version = version;
				this.sourceNodeId = sourceNodeId;
				this.nodeDatas = nodeDatas;
			}
		}
		public struct NodeData {
			public readonly string nodeId;
			public readonly string ip;
			public readonly int port;
			public readonly int priority;
			public NodeData (string nodeId, string ip, int port, int priority) {
				this.nodeId = nodeId;
				this.ip = ip;
				this.port = port;
				this.priority = priority;
			}
		}
		
		public static HelloData Hello (DisquuunOld.ByteDatas[] data) {
			var version = Encoding.UTF8.GetString(data[0].bytesArray[0]);
			var sourceNodeId = Encoding.UTF8.GetString(data[0].bytesArray[1]);
			var nodeDatas = new List<NodeData>();
			for (var i = 1; i < data.Length; i++) {
				var nodeIdStr = Encoding.UTF8.GetString(data[i].bytesArray[0]);
				var ipStr = Encoding.UTF8.GetString(data[i].bytesArray[1]);
				var portInt = Convert.ToInt16(Encoding.UTF8.GetString(data[i].bytesArray[2]));
				var priorityInt = Convert.ToInt16(Encoding.UTF8.GetString(data[i].bytesArray[3]));
				nodeDatas.Add(new NodeData(nodeIdStr, ipStr, portInt, priorityInt));
			}
			var helloData = new HelloData(version, sourceNodeId, nodeDatas.ToArray());
			return helloData;
		}
		
		public static int Qlen (DisquuunOld.ByteDatas[] data) {
			var qLenStr = Encoding.UTF8.GetString(data[0].bytesArray[0]);
			return Convert.ToInt32(qLenStr);
		}
		
// QSTAT,// <queue-name>
// QPEEK,// <queue-name> <count>
// ENQUEUE,// <job-id> ... <job-id>
// DEQUEUE,// <job-id> ... <job-id>
// DELJOB,// <job-id> ... <job-id>
// SHOW,// <job-id>
// QSCAN,// [COUNT <count>] [BUSYLOOP] [MINLEN <len>] [MAXLEN <len>] [IMPORTRATE <rate>]
// JSCAN,// [<cursor>] [COUNT <count>] [BUSYLOOP] [QUEUE <queue>] [STATE <state1> STATE <state2> ... STATE <stateN>] [REPLY all|id]
// PAUSE,// <queue-name> option1 [option2 ... optionN]
	
		
	}
		
		
}