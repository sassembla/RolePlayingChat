using System;
using System.Linq;
using DisquuunCore;
using DisquuunCore.Deserialize;

/*
	multiple execution.
*/

public partial class Tests {
	public void _3_0_2AsyncSocket (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var jobId1 = string.Empty;
		
		var queueId1 = Guid.NewGuid().ToString();
		disquuun.AddJob(queueId1, new byte[10]).Async(
			(command, data) => {
				jobId1 = DisquuunDeserializer.AddJob(data);
			}
		);
		
		var jobId2 = string.Empty;
		
		var queueId2 = Guid.NewGuid().ToString();
		disquuun.AddJob(queueId2, new byte[10]).Async(
			(command, data) => {
				jobId2 = DisquuunDeserializer.AddJob(data);
			}
		);
		
		WaitUntil(() => (!string.IsNullOrEmpty(queueId1) && !string.IsNullOrEmpty(queueId2)), 5);
		
		var done = false;
		disquuun.GetJob(new string[]{queueId1, queueId2}, "count", 2).Async(
			(command, data) => {
				var gets = DisquuunDeserializer.GetJob(data);
				
				Assert(2, gets.Length, "not match.");
				
				disquuun.FastAck(gets.Select(job => job.jobId).ToArray()).Async(
					(c, d) => {
						done = true;
					}
				);
			}	
		);
		
		WaitUntil(() => done, 5);
	}
	
	public void _3_1_MultipleAsyncSocket (Disquuun disquuun) {
		// WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		// var queueId1 = Guid.NewGuid().ToString();
		// var result1 = disquuun.AddJob(queueId1, new byte[10]).Sync();
		// var jobId1 = DisquuunDeserializer.AddJob(result1);
		
		// for () {		
		// 	var queueId = Guid.NewGuid().ToString();
		// 	var result = disquuun.AddJob(queueId, new byte[10]).Sync();
		// 	var jobId = DisquuunDeserializer.AddJob(result);
		// 	Assert(!string.IsNullOrEmpty(jobId), "empty.");
		// }
	}
	
	// 2つのAsync
	// 沢山のAsync
}