using System;
using System.Collections.Generic;

using DisquuunCore;
using DisquuunCore.Deserialize;

/*
	api sync tests.
*/

public partial class Tests {
	public void _1_0_AddJob (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var queueId = Guid.NewGuid().ToString();
		var result = disquuun.AddJob(queueId, new byte[10]).Sync();
		var jobId = DisquuunDeserializer.AddJob(result);
		Assert(!string.IsNullOrEmpty(jobId), "empty.");
	}
	
	public void _1_1_GetJob (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var queueId = Guid.NewGuid().ToString();
		
		disquuun.AddJob(queueId, new byte[10]).Sync();
		
		var result = disquuun.GetJob(new string[]{queueId}).Sync();
		var jobDatas = DisquuunDeserializer.GetJob(result);
		Assert(1, jobDatas.Length, "not match.");
	}
	
	public void _1_2_AckJob (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var queueId = Guid.NewGuid().ToString();
		var jobId = DisquuunDeserializer.AddJob(
			disquuun.AddJob(queueId, new byte[10]).Sync()
		);
		
		var result = disquuun.AckJob(new string[]{jobId}).Sync();
		var ackCount = DisquuunDeserializer.AckJob(result);
		Assert(1, ackCount, "not match.");
	}
	
	
	// ACKJOB,// jobid1 jobid2 ... jobidN
	// FASTACK,// jobid1 jobid2 ... jobidN
	// WORKING,// jobid
	// NACK,// <job-id> ... <job-id>
	// INFO,
	// HELLO,
	// QLEN,// <queue-name>
	// QSTAT,// <queue-name>
	// QPEEK,// <queue-name> <count>
	// ENQUEUE,// <job-id> ... <job-id>
	// DEQUEUE,// <job-id> ... <job-id>
	// DELJOB,// <job-id> ... <job-id>
	// SHOW,// <job-id>
	// QSCAN,// [COUNT <count>] [BUSYLOOP] [MINLEN <len>] [MAXLEN <len>] [IMPORTRATE <rate>]
	// JSCAN,// [<cursor>] [COUNT <count>] [BUSYLOOP] [QUEUE <queue>] [STATE <state1> STATE <state2> ... STATE <stateN>] [REPLY all|id]
	// PAUSE,
}