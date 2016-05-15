using System;
using System.Linq;
using DisquuunCore;
using DisquuunCore.Deserialize;

/*
	buffer over tests.
*/

public partial class Tests {
	public void _3_0_ByfferOverWithSingleSyncGetJob (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var queueId = Guid.NewGuid().ToString();
		
		disquuun.AddJob(queueId, new byte[disquuun.BufferSize]).Sync();
		
		var result = disquuun.GetJob(new string[]{queueId}).Sync();
		var jobDatas = DisquuunDeserializer.GetJob(result);
		Assert(1, jobDatas.Length, "not match.");
		
		// ack in.
		var jobIds = jobDatas.Select(job => job.jobId).ToArray();
		disquuun.FastAck(jobIds).Sync();
	}
	
	public void _3_1_ByfferOverWithMultipleSyncGetJob (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var queueId = Guid.NewGuid().ToString();
		
		var addJobCount = 2;
		for (var i = 0; i < addJobCount; i++) disquuun.AddJob(queueId, new byte[disquuun.BufferSize/addJobCount]).Sync();
		
		var result = disquuun.GetJob(new string[]{queueId}, "COUNT", addJobCount).Sync();
		var jobDatas = DisquuunDeserializer.GetJob(result);
		Assert(addJobCount, jobDatas.Length, "not match.");
		
		// ack in.
		var jobIds = jobDatas.Select(job => job.jobId).ToArray();
		disquuun.FastAck(jobIds).Sync();
	}
	
	public void _3_2_ByfferOverWithSokcetOverSyncGetJob (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var queueId = Guid.NewGuid().ToString();
		
		var addJobCount = 10001;
		for (var i = 0; i < addJobCount; i++) disquuun.AddJob(queueId, new byte[100]).Sync();
		
		var result = disquuun.GetJob(new string[]{queueId}, "COUNT", addJobCount).Sync();
		var jobDatas = DisquuunDeserializer.GetJob(result);
		Assert(addJobCount, jobDatas.Length, "not match.");
		
		// ack in.
		var jobIds = jobDatas.Select(job => job.jobId).ToArray();
		disquuun.FastAck(jobIds).Sync();
	}
}