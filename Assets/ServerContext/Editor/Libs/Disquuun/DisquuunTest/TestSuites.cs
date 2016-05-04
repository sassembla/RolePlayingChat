using System;
using System.Threading;
using DisquuunCore;
using DisquuunCore.Deserialize;

public class Test1_AllAPIs : TestBase {
	public override Action[] Ready (string testSuiteId) {
		return new Action[] {
			// add -> get -> ack.
			() => {
				jobQueueId = Guid.NewGuid().ToString();
			},
			() => disquuun.AddJob(jobQueueId, new byte[10]{0,1,2,3,4,5,6,7,8,9}, 0),
			() => disquuun.GetJob(new string[]{jobQueueId}),
			() => disquuun.AckJob(new string[]{latestGotJobId}),
			() => AssertResult("ACKJOB:1", latestResult, "add -> get -> ack."),
			
			// add -> get -> fastack.
			() => {
				jobQueueId = Guid.NewGuid().ToString();
			},
			() => disquuun.AddJob(jobQueueId, new byte[10]{0,1,2,3,4,5,6,7,8,9}, 0),
			() => disquuun.GetJob(new string[]{jobQueueId}),
			() => disquuun.FastAck(new string[]{latestGotJobId}),
			() => AssertResult("FASTACK:1", latestResult, "add -> get -> fastack."),
			
			// empty queue, will waiting data.
			() => {
				jobQueueId = Guid.NewGuid().ToString();
			},
			() => disquuun.GetJob(new string[]{jobQueueId}),
			() => disquuun2.AddJob(jobQueueId, new byte[10]{0,1,2,3,4,5,6,7,8,9}, 0),
			() => disquuun.FastAck(new string[]{latestGotJobId}),
			() => AssertResult("FASTACK:1", latestResult, "empty queue, will waiting data."),
			
			// non exist queue. never back until created.
			() => {
				jobQueueId = Guid.NewGuid().ToString();
			},
			() => disquuun.GetJob(new string[]{jobQueueId}),
			() => disquuun2.AddJob(jobQueueId, new byte[10]{0,1,2,3,4,5,6,7,8,9}, 0),
			() => disquuun.FastAck(new string[]{latestGotJobId}),
			() => AssertResult("FASTACK:1", latestResult, "non exist queue. never back until created."),
			
			// info blocking with empty queue.
			() => {
				jobQueueId = Guid.NewGuid().ToString();
			},
			() => disquuun.GetJob(new string[]{jobQueueId}),
			() => disquuun.Info(),
			() => disquuun2.AddJob(jobQueueId, new byte[10]{0,1,2,3,4,5,6,7,8,9}, 0),
			() => AssertResult("INFO:", latestResult, "info blocking with empty queue.1"),
			() => disquuun.FastAck(new string[]{latestGotJobId}),
			() => AssertResult("FASTACK:1", latestResult, "info blocking with empty queue.2"),
			
			// non blocking with empty queue.
			// () => disquuun.GetJob(new string[]{"testS"}, "NOHANG"),
			// () => disquuun.Info(),
			
			// info
			() => disquuun.Info(),
			() => AssertResult("INFO:", latestResult, "info"),
			
			// hello
			() => disquuun.Hello(),
			() => AssertResult("HELLO:", latestResult, "hello"),
			
			// qlen returns 0.
			() => {
				jobQueueId = Guid.NewGuid().ToString();
			},
			() => disquuun.Qlen(jobQueueId),
			() => AssertResult("QLEN:0", latestResult, "qlen returns 0."),
			
			// qlen returns 1.
			() => {
				jobQueueId = Guid.NewGuid().ToString();
			},
			() => disquuun.AddJob(jobQueueId, new byte[]{1}),
			() => disquuun.Qlen(jobQueueId),
			() => AssertResult("QLEN:1", latestResult, "qlen returns 1."),
			() => disquuun.GetJob(new string[]{jobQueueId}),
			() => disquuun.FastAck(new string[]{latestGotJobId}),
			
			// working return error with unused jobid.
			() => disquuun.Working("dummyJobId"),
			() => AssertFailureResult("BADID Invalid Job ID format.", latestError, "working return error with unused job id."),
			
			// working return \"seconds\".
			// () => disquuun.AddJob("working return \"seconds\"", new byte[]{1,3,5,7,9}),
			// () => disquuun.Working(latestAddedJobId),
			// () => AssertResult("WORKING:300", latestResult, "working return \"seconds\"."),
			// () => disquuun.GetJob(new string[]{"working return \"seconds\""}),
			// () => disquuun.FastAck(new string[]{latestGotJobId}),
			
			// nack return error with dummy job id.
			() => disquuun.Nack(new string[]{"dummyJobId"}),
			() => AssertFailureResult("BADID Invalid Job ID format.", latestError, "nack return error with dummy job id."),
			
			// nack succeeded with status.
			// まだうまくいってない気がする。
			// () => disquuun.AddJob("testNack", new byte[]{1,3,5,7,9}),
			// () => disquuun.Nack(new string[]{latestAddedJobId}),
			// () => AssertResult("NACK:1", latestResult, "nack succeeded with status."),
			// () => disquuun.GetJob(new string[]{"testNack"}),
			// () => disquuun.FastAck(new string[]{latestGotJobId}),
			
			() => {
				testLogger.Log("---------------------------result info.---------------------------");
				disquuun2.Info();
			},
			() => {
				disquuun.Disconnect();
				disquuun2.Disconnect();
			}
		};
	}
}

public class Test2_Fast : TestBase {
	public override Action[] Ready (string testSuiteId) {
		return new Action[] {
			() => testLogger.Log("test started. testSuiteId:" + testSuiteId),
			
			// info
			// () => disquuun.Info(),
			// () => AssertResult("INFO:", latestResult, "info"),
			
			// // hello
			// () => disquuun.Hello(),
			// () => AssertResult("HELLO:", latestResult, "hello"),
			
			// // () => 
			// // () => 
			// // () => 
			// // () => 
			
			// // multiple data in same time.
			// // () => {
			// // 	disquuun.Info();
			// // 	disquuun.Info();
			// // 	disquuun.Info();
			// // 	disquuun.Info();
			// // 	disquuun.Info();
			// // 	disquuun.Info();
			// // },
			
			// // some job.
			// () => {
			// 	jobQueueId = Guid.NewGuid().ToString();
			// },
			// () => {
			// 	for (var i = 0; i < 2; i++) {
			// 		disquuun2.AddJob(jobQueueId, new byte[]{0});
			// 	}
			// },
			// () => disquuun.GetJob(new string[]{jobQueueId}, "COUNT", 10),
			// () => disquuun.FastAck(latestWholeGotJobId),
			// () => AssertResult("FASTACK:2", latestResult, "some job."),
			
			// mass job.
			() => {
				jobQueueId = Guid.NewGuid().ToString();
			},
			() => {
				for (var i = 0; i < 1000; i++) {
					disquuun.AddJob(jobQueueId, new byte[]{0});
				}
			},
			() => disquuun.GetJob(new string[]{jobQueueId}, "COUNT", 1000),
			() => {
				if (latestWholeGotJobId != null) {
					testLogger.Log("latestWholeGotJobId:" + latestWholeGotJobId.Length);
					disquuun.FastAck(latestWholeGotJobId);
				} else {
					testLogger.Log("should wait... maybe getJob is not finished yet.");
				}
			},
			() => AssertResult("FASTACK:1000", latestResult, "mass job."),
			
			() => {
				testLogger.Log("---------------------------result info.---------------------------");
				disquuun2.Info();
			},
			
			() => {
				disquuun.Disconnect();
				disquuun2.Disconnect();
			}
		};
	}
}

public class Test3_Size : TestBase {
	public override void JobProcess (Disquuun.DisqueCommand command, DisquuunCore.Disquuun.ByteDatas[] byteDatas) {
		try {
			testLogger.Log("// data received:" + command + " byteDatas:" + byteDatas.Length);
			
			switch (command) {
				case Disquuun.DisqueCommand.ADDJOB: {
					var addedJobId = DisquuunDeserializer.AddJob(byteDatas);
					testLogger.Log("addedJobId:" + addedJobId);
					latestAddedJobId = addedJobId;
					latestResult = "ADDJOB:OK";
					break;
				}
				case Disquuun.DisqueCommand.GETJOB: {
					var jobDatas = DisquuunDeserializer.GetJob(byteDatas);
					foreach (var jobData in jobDatas) {
						var gotJobIdStr = jobData.jobId;
						// testLogger.Log("gotJobIdStr:" + gotJobIdStr);
						
						latestGotJobId = gotJobIdStr;
					}
					
					latestWholeGotJobId = new string[jobDatas.Length];
					for (var i = 0; i < jobDatas.Length; i++) {
						latestWholeGotJobId[i] = jobDatas[i].jobId;
					}
					latestResult = "GETJOB:" + jobDatas.Length;
					waiting = false;
					break;
				}
				case Disquuun.DisqueCommand.ACKJOB: {
					var result = DisquuunDeserializer.AckJob(byteDatas);
					// testLogger.Log("ackjob result:" + result);
					latestResult = "ACKJOB:" + result;
					break;
				}
				case Disquuun.DisqueCommand.FASTACK: {
					var result = DisquuunDeserializer.FastAck(byteDatas);
					// testLogger.Log("fastack result:" + result);
					latestResult = "FASTACK:" + result;
					waiting = false;
					break;
				}
				case Disquuun.DisqueCommand.WORKING: {
					var postponeSec = DisquuunDeserializer.Working(byteDatas);
					// testLogger.Log("working postponeSec:" + postponeSec);
					latestResult = "WORKING:" + postponeSec;
					break;
				}
				case Disquuun.DisqueCommand.NACK: {
					var result = DisquuunDeserializer.Nack(byteDatas);
					// testLogger.Log("nack result:" + result);
					latestResult = "NACK:" + result;
					break;
				}			
				case Disquuun.DisqueCommand.INFO: {
					var infoStr = DisquuunDeserializer.Info(byteDatas);
					testLogger.Log("infoStr:" + infoStr);
					latestResult = "INFO:";
					break;
				}
				case Disquuun.DisqueCommand.HELLO: {
					var helloData = DisquuunDeserializer.Hello(byteDatas);
					// testLogger.Log("helloData	vr:" + helloData.version);
					// testLogger.Log("helloData	id:" + helloData.sourceNodeId);
					
					// testLogger.Log("helloData	node Id:" + helloData.nodeDatas[0].nodeId);
					// testLogger.Log("helloData	node ip:" + helloData.nodeDatas[0].ip);
					// testLogger.Log("helloData	node pt:" + helloData.nodeDatas[0].port);
					// testLogger.Log("helloData	node pr:" + helloData.nodeDatas[0].priority);
					latestResult = "HELLO:";
					break;
				}
				case Disquuun.DisqueCommand.QLEN: {
					var qLengthInt = DisquuunDeserializer.Qlen(byteDatas);
					// testLogger.Log("qLengthInt:" + qLengthInt);
					latestResult = "QLEN:" + qLengthInt;
					break;
				}
				
				// QSTAT,// <queue-name>
				// QPEEK,// <queue-name> <count>
				// ENQUEUE,// <job-id> ... <job-id>
				// DEQUEUE,// <job-id> ... <job-id>
				// DELJOB,// <job-id> ... <job-id>
				// SHOW,// <job-id>
				// QSCAN,// [COUNT <count>] [BUSYLOOP] [MINLEN <len>] [MAXLEN <len>] [IMPORTRATE <rate>]
				// JSCAN,// [<cursor>] [COUNT <count>] [BUSYLOOP] [QUEUE <queue>] [STATE <state1> STATE <state2> ... STATE <stateN>] [REPLY all|id]
				// PAUSE,
				default: {
					// ignored
					break;
				}
			}
		} catch (Exception e) {
			testLogger.Log("e:" + e);
		}
	}
	
	
	public override Action[] Ready (string testSuiteId) {
		return new Action[] {
			() => testLogger.Log("test started. testSuiteId:" + testSuiteId),
			
			// size is over maximum.
			() => jobQueueId = Guid.NewGuid().ToString(),
			() => disquuun.AddJob(jobQueueId, new byte[disquuun2.BufferSize-106]),
			() => {
				waiting = true;
				disquuun2.GetJob(new string[]{jobQueueId});
			},
			() => AssertResult("GETJOB:1", latestResult, "size is over maximum."),
			() => {
				waiting = true;
				disquuun.FastAck(latestWholeGotJobId);
			},
			
			
			// size is over maximum2.
			() => jobQueueId = Guid.NewGuid().ToString(),
			() => disquuun.AddJob(jobQueueId, new byte[disquuun2.BufferSize-106]),
			() => {
				waiting = true;
				disquuun2.GetJob(new string[]{jobQueueId});
			},
			() => AssertResult("GETJOB:1", latestResult, "size is over maximum2."),
			() => {
				waiting = true;
				disquuun.FastAck(latestWholeGotJobId);
			},
			
			
			() => {
				testLogger.Log("---------------------------result info.---------------------------");
				disquuun.Info();
			},
			
			
			() => {
				disquuun.Disconnect();
				disquuun2.Disconnect();
			}
		};
	}
}