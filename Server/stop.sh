sudo bin/sbin/nginx -p $(pwd)/bin -s stop

ps aux | grep [d]isque-server | awk '{print $2}' | xargs sudo kill -9