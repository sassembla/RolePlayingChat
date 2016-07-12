using System;

using DisquuunCore;
using DisquuunCore.Deserialize;

/*
	pipeline tests.
*/

public partial class Tests {
	private object _0_9_0_PipelineCommandsObject = new object();

	public void _0_9_0_PipelineCommands (Disquuun disquuun) {
		WaitUntil("_0_9_0_PipelineCommands", () => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);

		var infoCount = 0;

		disquuun.Pipeline(
			disquuun.Info(), disquuun.Info()
		).Execute( 
			(command, data) => {
				lock (_0_9_0_PipelineCommandsObject) infoCount++;
			}
		);

		WaitUntil("_0_9_0_PipelineCommands", () => (infoCount == 2), 5);
	}

	private object _0_9_1_MultiplePipelinesObject = new object(); 
	
	public void _0_9_1_MultiplePipelines (Disquuun disquuun) {
		WaitUntil("_0_9_1_MultiplePipelines", () => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);

		var infoCount = 0;

		disquuun.Pipeline(disquuun.Info());
		disquuun.Pipeline(disquuun.Info());
		disquuun.Pipeline(disquuun.Info()).Execute( 
			(command, data) => {
				lock (_0_9_1_MultiplePipelinesObject) infoCount++;
			}
		);

		WaitUntil("_0_9_1_MultiplePipelines", () => (infoCount == 3), 5);
	}

	private object _0_9_2_MultipleCommandPipelinesObject = new object();

	public void _0_9_2_MultipleCommandPipelines (Disquuun disquuun) {
		WaitUntil("_0_9_2_MultipleCommandPipelines", () => (disquuun.State() == Disquuun.ConnectionState.OPENED), 5);

		var infoCount = 0;
		var addedJobId = string.Empty;
		var gotJobId = "_";

		var queueId = Guid.NewGuid().ToString();

		disquuun.Pipeline(disquuun.Info());
		disquuun.Pipeline(disquuun.AddJob(queueId, new byte[100]));
		disquuun.Pipeline(disquuun.GetJob(new string[]{queueId})).Execute( 
			(command, data) => {
				switch (command) {
					case DisqueCommand.INFO: {
						lock (_0_9_2_MultipleCommandPipelinesObject) {
							TestLogger.Log("1", true);
							infoCount++;
						}
						break;
					}
					case DisqueCommand.ADDJOB: {
						lock (_0_9_2_MultipleCommandPipelinesObject) {
							TestLogger.Log("2", true);
							addedJobId = DisquuunDeserializer.AddJob(data);
						} 
						break;
					}
					case DisqueCommand.GETJOB: {
						lock (_0_9_2_MultipleCommandPipelinesObject) {
							TestLogger.Log("3", true);
							var gotJobDatas = DisquuunDeserializer.GetJob(data);
							gotJobId = gotJobDatas[0].jobId;
							disquuun.FastAck(new string[]{gotJobId}).DEPRICATED_Sync();
						}
						break;
					}
				}
			}
		);

		WaitUntil("_0_9_2_MultipleCommandPipelines", () => (infoCount == 1 && !string.IsNullOrEmpty(addedJobId) && gotJobId == addedJobId), 5);
	}
}