using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DisquuunCore;
using DisquuunCore.Deserialize;

/*
	benchmark
*/

public partial class Tests {
	public void _7_0_AddJob1000 (Disquuun disquuun) {
		WaitUntil("_7_0_AddJob1000", () => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var count = 1000;
		
		var connectedCount = 0;
		disquuun = new Disquuun(DisquuunTests.TestDisqueHostStr, DisquuunTests.TestDisquePortNum, 1024, 10,
			disquuunId => {
				connectedCount++;
			},
			(info, e) => {
				TestLogger.Log("error, info:" + info + " e:" + e.Message);
			}
		);
		
		
		WaitUntil("_7_0_AddJob1000", () => (connectedCount == 1), 5);
		
		var addedCount = 0;
		
		var queueId = Guid.NewGuid().ToString();
		
		var w = new Stopwatch();
		w.Start();
		for (var i = 0; i < count; i++) {
			disquuun.AddJob(queueId, new byte[10]).Async(
				(command, data) => {
					lock (this) addedCount++;
				}
			);
		}
		w.Stop();
		TestLogger.Log("_7_0_AddJob1000 w:" + w.ElapsedMilliseconds + " tick:" + w.ElapsedTicks);
		
		WaitUntil("_7_0_AddJob1000", () => (addedCount == count), 100);
		
		var gotCount = 0;
		disquuun.GetJob(new string[]{queueId}, "count", count).Async(
			(command, data) => {
				var jobDatas = DisquuunDeserializer.GetJob(data);
				var jobIds = jobDatas.Select(j => j.jobId).ToArray();
				disquuun.FastAck(jobIds).Async(
					(c2, d2) => {
						var fastackCount = DisquuunDeserializer.FastAck(d2);
						lock (this) {
							gotCount += fastackCount;
						}
					}
				);
			}
		);
		
		WaitUntil("_7_0_AddJob1000", () => (gotCount == count), 10);
		disquuun.Disconnect(true);
	}

	public void _7_0_0_AddJob1000by100Connectoion (Disquuun disquuun) {
		WaitUntil("_7_0_0_AddJob1000by100Connectoion", () => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var count = 1000;
		
		var connectedCount = 0;
		disquuun = new Disquuun(DisquuunTests.TestDisqueHostStr, DisquuunTests.TestDisquePortNum, 1024, 100,
			disquuunId => {
				connectedCount++;
			},
			(info, e) => {
				TestLogger.Log("error, info:" + info + " e:" + e.Message);
			}
		);
		
		
		WaitUntil("_7_0_0_AddJob1000by100Connectoion", () => (connectedCount == 1), 5);
		
		var addedCount = 0;
		
		var queueId = Guid.NewGuid().ToString();
		
		var w = new Stopwatch();
		w.Start();
		for (var i = 0; i < count; i++) {
			disquuun.AddJob(queueId, new byte[10]).Async(
				(command, data) => {
					lock (this) addedCount++;
				}
			);
		}
		w.Stop();
		TestLogger.Log("_7_0_0_AddJob1000by100Connectoion w:" + w.ElapsedMilliseconds + " tick:" + w.ElapsedTicks);
		
		WaitUntil("_7_0_0_AddJob1000by100Connectoion", () => (addedCount == count), 100);
		
		var gotCount = 0;
		disquuun.GetJob(new string[]{queueId}, "count", count).Async(
			(command, data) => {
				var jobDatas = DisquuunDeserializer.GetJob(data);
				var jobIds = jobDatas.Select(j => j.jobId).ToArray();
				disquuun.FastAck(jobIds).Async(
					(c2, d2) => {
						var fastackCount = DisquuunDeserializer.FastAck(d2);
						lock (this) {
							gotCount += fastackCount;
						}
					}
				);
			}
		);
		
		WaitUntil("_7_0_0_AddJob1000by100Connectoion", () => (gotCount == count), 10);
		disquuun.Disconnect(true);
	}
	
	private object _7_1_GetJob1000Lock = new object();

	public void _7_1_GetJob1000 (Disquuun disquuun) {
		WaitUntil("_7_1_GetJob1000", () => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var addingJobCount = 1000;
		
		var connected = false;
		disquuun = new Disquuun(DisquuunTests.TestDisqueHostStr, DisquuunTests.TestDisquePortNum, 1024, 10,
			disquuunId => {
				connected = true;
			},
			(info, e) => {
				TestLogger.Log("error, info:" + info + " e:" + e.Message, true);
				throw e;
			}
		);
		
		var r0 = WaitUntil("r0 _7_1_GetJob1000", () => connected, 5);
		if (!r0) return;

		TestLogger.Log("------------------------------CONNECTED", true);

		var addedCount = 0;
		
		var queueId = Guid.NewGuid().ToString();
		var lockObject = new object();

		for (var i = 0; i < addingJobCount; i++) {
			disquuun.AddJob(queueId, new byte[10]).Async(
				(command, data) => {
					lock (lockObject) addedCount++;
				}
			);
		}
		
		var r1 = WaitUntil("r1 _7_1_GetJob1000", () => (addedCount == addingJobCount), 10);
		if (!r1) return;
		
		TestLogger.Log("------------------------------ADDED", true);
		

		var gotJobDataIds = new List<string>();
		
		
		var w = new Stopwatch();
		w.Start();
		for (var i = 0; i < addingJobCount; i++) {
			disquuun.GetJob(new string[]{queueId}).Async(
				(command, data) => {
					lock (_7_1_GetJob1000Lock) {
						var jobDatas = DisquuunDeserializer.GetJob(data);
						var jobIds = jobDatas.Select(j => j.jobId).ToList();
						gotJobDataIds.AddRange(jobIds);
					}
				}
			);
		}

		var r2 = WaitUntil("r2 _7_1_GetJob1000", () => (gotJobDataIds.Count == addingJobCount), 10);
		if (!r2) {
			TestLogger.Log("gotJobDataIds:" + gotJobDataIds.Count, true);
			return;
		}

		w.Stop();
		TestLogger.Log("_7_1_GetJob1000 w:" + w.ElapsedMilliseconds + " tick:" + w.ElapsedTicks);
		
		var result = DisquuunDeserializer.FastAck(disquuun.FastAck(gotJobDataIds.ToArray()).DEPRICATED_Sync());
		
		Assert("_7_1_GetJob1000", addingJobCount, result, "result not match.");
		disquuun.Disconnect(true);
	}
	
	public void _7_1_0_GetJob1000by100Connection (Disquuun disquuun) {
		WaitUntil("_7_1_0_GetJob1000by100Connection", () => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);
		
		var addingJobCount = 1000;
		
		var connected = false;
		disquuun = new Disquuun(DisquuunTests.TestDisqueHostStr, DisquuunTests.TestDisquePortNum, 1024, 100,
			disquuunId => {
				connected = true;
			},
			(info, e) => {
				TestLogger.Log("error, info:" + info + " e:" + e.Message);
			}
		);
		
		WaitUntil("_7_1_0_GetJob1000by100Connection", () => connected, 5);
		
		var addedCount = 0;
		
		var queueId = Guid.NewGuid().ToString();
		
		for (var i = 0; i < addingJobCount; i++) {
			disquuun.AddJob(queueId, new byte[10]).Async(
				(command, data) => {
					lock (this) addedCount++;
				}
			);
		}

		
		WaitUntil("_7_1_0_GetJob1000by100Connection", () => (addedCount == addingJobCount), 10);
		
		var gotJobDataIds = new List<string>();
		
		
		var w = new Stopwatch();
		w.Start();
		for (var i = 0; i < addingJobCount; i++) {
			disquuun.GetJob(new string[]{queueId}).Async(
				(command, data) => {
					lock (this) {
						var jobDatas = DisquuunDeserializer.GetJob(data);
						var jobIds = jobDatas.Select(j => j.jobId).ToList();
						gotJobDataIds.AddRange(jobIds);
					}
				}
			);
		}
		
		WaitUntil("_7_1_0_GetJob1000by100Connection", () => (gotJobDataIds.Count == addingJobCount), 10);
		

		w.Stop();
		TestLogger.Log("_7_1_0_GetJob1000by100Connection w:" + w.ElapsedMilliseconds + " tick:" + w.ElapsedTicks);
		
		var result = DisquuunDeserializer.FastAck(disquuun.FastAck(gotJobDataIds.ToArray()).DEPRICATED_Sync());
		
		Assert("_7_1_0_GetJob1000by100Connection", addingJobCount, result, "result not match.");
		disquuun.Disconnect(true);
	}
}