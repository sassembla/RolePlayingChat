# このへんの機能を、CrossPeerとして実装する。

# 対象のフォルダを見つけてくる機能
cp -Rp ./Assets/XrossPeer/Disquuun ./CoreCLR/

# このへんは蛇足だな〜
ps aux | grep [d]isque-server | awk '{print $2}' | xargs sudo kill -9
nohup ./Server/disque/src/disque-server > /dev/null 2>&1 &

# 実行 ここまではやろう。
cd CoreCLR
dotnet run
cd ../