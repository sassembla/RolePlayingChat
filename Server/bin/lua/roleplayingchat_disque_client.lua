-- get identity of game from url. e.g. http://somewhere/game_key -> game_key
local identity = string.gsub (ngx.var.uri, "/", "")

-- generate identity of queue for target context.
IDENTIFIER_CONTEXT = identity .. "_context"


-- identifier-client = UUID. e.g. AD112CD4-3A23-4E49-B562-E07A360DD836 len is 36.

STATE_CONNECT			= 1
STATE_STRING_MESSAGE	= 2
STATE_BINARY_MESSAGE	= 3
STATE_DISCONNECT_INTENT	= 4
STATE_DISCONNECT_ACCIDT = 5
STATE_DISCONNECT_DISQUE_ACKFAILED = 6
STATE_DISCONNECT_DISQUE_ACCIDT_SENDFAILED = 7

local playerId = ngx.req.get_headers()["playerId"]
if not playerId then
	playerId = "_empty_"
end


ip = "127.0.0.1"
port = 7711


-- このへんで、サーバに問い合わせるみたいなのができる。authかけるならここ。かけないと、解析で赤の他人が接続することが可能。必ずRedisとかにhash化されたキーを入れておいて、合わさせること。

-- entrypoint for WebSocket client connection.


-- setup Disque get-add
local disque = require "disque.disque"

local uuid = require "uuid.uuid"
local time = ngx.now() * 1000 --millisecond,, should add other parameter. -> playerRandom?
uuid:setRandomSeed(time)

local connectionId = uuid:getUUID()

receiveJobConn = disque:new()
local ok, err = receiveJobConn:connect(ip, port)
if not ok then
	ngx.log(ngx.ERR, "connection:", connectionId, " failed to generate receiveJob client")
	return
end

receiveJobConn:set_timeout(1000 * 60 * 60)


addJobCon = disque:new()
local ok, err = addJobCon:connect(ip, port)
if not ok then
	ngx.log(ngx.ERR, "connection:", connectionId, " failed to generate addJob client")
	return
end


-- setup websocket client
local wsServer = require "ws.websocketServer"

ws, wErr = wsServer:new{
	timeout = 10000000,-- this should be set good value.
	max_payload_len = 65535
}

if not ws then
	ngx.log(ngx.ERR, "connection:", connectionId, " failed to new websocket: ", wErr)
	return
end

ngx.log(ngx.ERR, "connection:", connectionId, " start connect.")

function connectWebSocket()
	-- start receiving message from context.
	ngx.thread.spawn(contextReceiving)

	ngx.log(ngx.ERR, "connection:", connectionId, " established. playerId:", playerId, " to context:", IDENTIFIER_CONTEXT)

	-- send connected to gameContext.
	local data = STATE_CONNECT..connectionId..playerId
	addJobCon:addjob(IDENTIFIER_CONTEXT, data, 0)

	-- start websocket serving.
	while true do
		local recv_data, typ, err = ws:recv_frame()

		if ws.fatal then
			ngx.log(ngx.ERR, "connection:", connectionId, " closing accidentially. ", err)
			local data = STATE_DISCONNECT_ACCIDT..connectionId..playerId
			addJobCon:addjob(IDENTIFIER_CONTEXT, data, 0)
			break
		end

		if not recv_data then
			ngx.log(ngx.ERR, "connection:", connectionId, " received empty data.")
			-- log only. do nothing.
		end

		if typ == "close" then
			ngx.log(ngx.ERR, "connection:", connectionId, " closing intentionally.")
			local data = STATE_DISCONNECT_INTENT..connectionId..playerId
			addJobCon:addjob(IDENTIFIER_CONTEXT, data, 0)
			
			-- start close.
			break
		elseif typ == "ping" then
			local bytes, err = ws:send_pong()
			ngx.log(ngx.ERR, "connection:", serverId, " ping received.")
			if not bytes then
				ngx.log(ngx.ERR, "connection:", serverId, " failed to send pong: ", err)
				break
			end

		elseif typ == "pong" then
			ngx.log(ngx.INFO, "client ponged")

		elseif typ == "text" then
			-- post message to central.
			local data = STATE_STRING_MESSAGE..connectionId..recv_data
			addJobCon:addjob(IDENTIFIER_CONTEXT, data, 0)
		elseif typ == "binary" then
			-- post binary data to central.
			local binData = STATE_BINARY_MESSAGE..connectionId..recv_data
			addJobCon:addjob(IDENTIFIER_CONTEXT, binData, 0)
		end
	end

	ws:send_close()
	ngx.log(ngx.ERR, "connection:", connectionId, " connection closed")

	ngx.exit(200)
end

-- loop for receiving messages from game context.
function contextReceiving ()
	local localWs = ws
	while true do
		-- receive message from disque queue, through connectionId. 
		-- game context will send message via connectionId.
		local res, err = receiveJobConn:getjob("from", connectionId)
		
		if not res then
			ngx.log(ngx.ERR, "err:", err)
			break
		else
			local datas = res[1]
			-- ngx.log(ngx.ERR, "client datas1:", datas[1])-- connectionId
			-- ngx.log(ngx.ERR, "client datas2:", datas[2])-- messageId
			-- ngx.log(ngx.ERR, "client datas3:", datas[3])-- data
			local messageId = datas[2]
			local sendingData = datas[3]
			
			-- fastack to disque
			local ackRes, ackErr = receiveJobConn:fastack(messageId)
			if not ackRes then
				ngx.log(ngx.ERR, "disque, ackに失敗したケース connection:", connectionId, " ackErr:", ackErr)				
				local data = STATE_DISCONNECT_DISQUE_ACKFAILED..connectionId..playerId
				addJobCon:addjob(IDENTIFIER_CONTEXT, data, 0)
				break
			end
			-- ngx.log(ngx.ERR, "messageId:", messageId, " ackRes:", ackRes)

			-- というわけで、ここまででデータは取得できているが、ここで先頭を見て、、みたいなのが必要になってくる。
			-- 入れる側にもなんかデータ接続が出ちゃうんだなあ。うーん、、まあでもサーバ側なんでいいや。CopyがN回増えるだけだ。
			-- 残る課題は、ここでヘッダを見る、ってことだね。

			-- send data to client
			local bytes, err = localWs:send_binary(sendingData)

			if not bytes then
				ngx.log(ngx.ERR, "disque, 未解決の、送付失敗時にすべきこと。 connection:", connectionId, " failed to send text to client. err:", err)
				local data = STATE_DISCONNECT_DISQUE_ACCIDT_SENDFAILED..connectionId..sendingData
				addJobCon:addjob(IDENTIFIER_CONTEXT, data, 0)
				break
			end
		end
	end
	
	ngx.log(ngx.ERR, "connection:", connectionId, " connection closed by disque error.")
	ngx.exit(200)
end

connectWebSocket()


-- 別の話、ここに受け入れバッファを持つことは可能か

-- -> なんか切断時コンテキスト混同イレギュラーがあったんだよな〜〜あれの原因探さないとなー
-- 何が起きていたかっていうと、切断確認が別のクライアントのものをつかんでいた、っていうやつで、
-- 受け取り時にコネクション状態を見るとおかしくなっている、ていうやつ。
-- 、、、コネクション状態に関して見るフラッグをngx.thread内で扱ってはいけない、みたいなのがありそう。
-- ということは、それ以外であれば混同しないのでは。

-- それが解消したらできそうかな？できそうだな。
-- パラメータを保持させて、か、、まあ親のインスタンスのパラメータに触れるのはしんどいんで、やっぱりluaだと厳しいねっていう話になるのがいい気がする。
-- 本当にあると嬉しいのは、TCP以外が喋れる、フロントになれるメッセージキューか。





