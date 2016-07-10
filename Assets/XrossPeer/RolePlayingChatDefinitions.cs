public class Develop {
	public const string TIME_ASSERT = "2016/3/20 12:29:52";
}

public class RolePlayingChatDefinitions {
	public const int FRAMERATE = 60;
	
	public const float FloorUnit = 10f;// 適当に地面のユニットを作った。10f x 10fのサイズ。
	public const float StepUnit = 0.05f;
	
	public const float TalkRange = 0.5f;// 超適当。
}


public enum BattleState : int {
	STATE_READY,
	STATE_NOPLAYERS,
	STATE_PLAYERS_EXISTS,
	STATE_ENDING,
	STATE_ENDED,
}

