cp -Rp ./Assets/ServerContext/Editor/Libs/Disquuun ./CoreCLR/

ps aux | grep [d]isque-server | awk '{print $2}' | xargs sudo kill -9
nohup ./Server/disque/src/disque-server > /dev/null 2>&1 &

cd CoreCLR
dotnet run
cd ../