public class Develop {
	public const string TIME_ASSERT = "2016/3/20 12:29:52";
}

public class CaliversDefinitions {
	public const bool RUN_IN_UNITY = false;
	public const int FRAMERATE = 60;
	public const bool DUMMY_USEDELAY = false;
	public const int DUMMY_DELAY = 100;// ms


	public const float VISUAL_SCALE_RATE = 0.01f;

	public const string PLAYER_PREFIX_DUMMY = "dummyId_";

	public const string CONNNECTION_STATUS_ALIVE = "alive";
	public const string CONNNECTION_STATUS_DEAD = "dead";

	public const int PLAYER_LIFE_DEFAULT = 100;

	public const int PLAYER_CALIVER_FRAME_DEFAULT = 10;
	
	public const int PLAYER_ATTACK_POWER_DEFAULT = 60;
	public const int PLAYER_ATTACK_FRAME_DEFAULT = 120;

	public const int PLAYER_STEP_SIZE_DEFAULT = 100;
	public const int PLAYER_STEP_FRAME_DEFAULT = 80;
	
	public const int SPAWN_USE_FRAME = 60;// 1.0秒
}


public enum BattleState : int {
	STATE_READY,
	STATE_NOPLAYERS,
	STATE_PLAYERS_EXISTS,
	STATE_ENDING,
	STATE_ENDED,
}

public enum PlayerSide : int {
	PLAYER_SIDE_A,
	PLAYER_SIDE_B,
	PLAYER_SIDE_C,
	PLAYER_SIDE_D,

	DUMMY_SIDE_A,
	DUMMY_SIDE_B,
	DUMMY_SIDE_C,
	DUMMY_SIDE_D,
}

public enum PlayerKind : int {
	KIND_PLAYER,
	KIND_AI
}



public enum AutoOrder {
	CONTINUE,

	DEFAULT_0,
	
	SPAWN_0,

	MOVE_0,

	CALIVER_0,CALIVER_1,// とりあえずこの書き方にしておくけど、後で問題が出そう。
	
	ATTACK_0,


	// dummy
	DUMMY_MOVE_0,
}


