using UnityEditor;
using System;

/**
	ブートストラップ設定できたんで、あとはここに対してどう干渉するか。
	
*/
namespace XrossPeerUtility {

    /*
		this method will call when Unity compiled Editor code.
	*/
    [InitializeOnLoad] public class XrossPeerBootstrap {
		private static XrossPeerBootstrap bootstrap;
		
		static XrossPeerBootstrap () {
			bootstrap = new XrossPeerBootstrap();
		}
		
		private XrossPeerBootstrap () {
			EditorApplication.playmodeStateChanged += DetectPlayStart;
			EditorApplication.update += DetectCompileStart;

			// handler for setup XrossPeer.
			Setup();
		}
		
		private void DetectPlayStart () {
			if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode) {
				EditorApplication.playmodeStateChanged -= DetectPlayStart;

				// handler for teardown XrossPeer.
				bootstrap.Teardown();
			}
		}
		
		private void DetectCompileStart () {
			if (EditorApplication.isCompiling) {
				EditorApplication.update -= DetectCompileStart;
				
				// handler for teardown XrossPeer.
				bootstrap.Teardown();
			}
		}
		


		private Action Setup = () => {
			// 特定のコードを書く感じかな〜〜外部から扱えると良いんだが。GUIでPeer間のコピーとかを実現できると良いと思うんだけどな。
		};
		
		private Action Teardown = () => {
			// イベントのやつを足す、っていうのを外部に書く感じかな。現在あるこのコードが外部、っていう扱いで良いと思うんだよな〜〜。
			// ほかのところに書くためのスペースを用意する感じになるのかな〜〜。
			// もうちょっと書き方を特定できるようにしよう。なんかメソッド用意するのが良いか。でも各Peerのメソッド呼びたいし、
			// そのメソッドの形も、「初期化」「停止」とかパターンつくれそうな気がするんで、やっぱりGUIから調整できた方が良いな。
			// 独自メソッドを書く場合は、何かしらメソッド名からリフレクションかな？それはUnity内のみ、って感じかな〜〜〜。特定のメソッドが番号で、でも良いかもしれない。
			// 1,2,3,4,5とか。メソッド内は自分で書いてね、っていう。まあやりすぎてるからいいや。自分は使わなそうだし。
			// じゃあやめよう。
		};
	}
}