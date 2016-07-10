using System;

public class UnityEditorUpdateExecutor {
	private readonly Action UpdateExe;

	public UnityEditorUpdateExecutor (Action UpdateExe) {
		this.UpdateExe = UpdateExe;
	}

	public void Update () {
		UpdateExe();
	}
}