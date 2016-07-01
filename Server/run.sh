sudo bin/sbin/nginx -p $(pwd)/bin

ps aux | grep [d]isque-server | awk '{print $2}' | xargs sudo kill -9
# redis-server /usr/local/etc/redis.conf
nohup ./disque/src/disque-server > /dev/null 2>&1 &