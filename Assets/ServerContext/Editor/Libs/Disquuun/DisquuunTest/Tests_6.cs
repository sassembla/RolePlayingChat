using System;
using System.Collections.Generic;
using System.Linq;
using DisquuunCore;
using DisquuunCore.Deserialize;

/*
	slot over tests.
*/

public partial class Tests {
	public void _6_0_ExceededSocketNo3In2 (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var queueId = Guid.NewGuid().ToString();
		var infoCount = 0;
		
		for (var i = 0; i < 3; i++) {
			disquuun.Info().Async(
				(command, data) => {
					infoCount++;
				}
			);
		}
		
		WaitUntil(() => (infoCount == 3), 5);
	}
	
	public void _6_1_ExceededSocketNo100In2 (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var queueId = Guid.NewGuid().ToString();
		var infoCount = 0;
		
		var connectCount = 1000;
		
		for (var i = 0; i < connectCount; i++) {
			disquuun.Info().Async(
				(command, data) => {
					lock (this) infoCount++;
				}
			);
		}
		
		WaitUntil(() => (infoCount == connectCount), 5);
	}

	public void _6_2_LargeSizeSendThenSmallSizeSendMakeEmitOnSendAfterOnReceived (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		for (var i = 0; i < 10; i++) {
			Disquuun.Log("_6_2_LargeSizeSendThenSmallSizeSendMakeEmitOnSendAfterOnReceived_i_" + i);
			var queueId = Guid.NewGuid().ToString();
			
			var sended = false;
			disquuun.AddJob(queueId, new byte[40000]).Async(
				(command, data) => {
					disquuun.AddJob(queueId, new byte[100]).Async(
						(command2, data2) => {
							sended = true;
						}	
					);
				}
			);

			WaitUntil(() => (sended), 5);

			var fastacked = false;
			disquuun.GetJob(new string[]{queueId}, "count", 2).Async(
				(command, data) => {
					var jobDatas = DisquuunDeserializer.GetJob(data);
					var jobIds = jobDatas.Select(j => j.jobId).ToArray();
					disquuun.FastAck(jobIds).Async(
						(command2, data2) => {
							fastacked = true;
						}
					);
				}
			);

			WaitUntil(() => fastacked, 5);
		}
	}

	public void _6_3_LargeSizeSendThenSmallSizeSendLoopMakeEmitOnSendAfterOnReceived (Disquuun disquuun) {
		WaitUntil(() => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		for (var i = 0; i < 10; i++) {
			Disquuun.Log("_6_3_LargeSizeSendThenSmallSizeSendLoopMakeEmitOnSendAfterOnReceived_i:" + i);
			var queueId = Guid.NewGuid().ToString();
			
			var index = 0;
			var bytes = new List<byte[]>();
			
			bytes.Add(new byte[40000]);
			bytes.Add(new byte[100]);

			disquuun.AddJob(queueId, bytes[index]).Loop(
				(command, data) => {
					index++;
					if (bytes.Count <= index) return false;
					return true;
				}
			);

			WaitUntil(() => (index == 2), 5);
			
			var fastacked = false;
			disquuun.GetJob(new string[]{queueId}, "count", 2).Async(
				(command, data) => {
					var jobDatas = DisquuunDeserializer.GetJob(data);
					var jobIds = jobDatas.Select(j => j.jobId).ToArray();
					disquuun.FastAck(jobIds).Async(
						(command2, data2) => {

							fastacked = true;
						}
					);
				}
			);
			
			WaitUntil(() => fastacked, 5);
		}
	}
	
}