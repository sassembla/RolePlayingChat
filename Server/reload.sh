
sudo ./bin/sbin/nginx -s reload

ps aux | grep [d]isque-server | awk '{print $2}' | xargs sudo kill -9
nohup ./disque/src/disque-server > /dev/null 2>&1 &