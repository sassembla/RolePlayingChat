IDENTIFIER_CONTEXT = "tocontext"
-- identifier-client = UUID. e.g. AD112CD4-3A23-4E49-B562-E07A360DD836 len is 36.

HEADER_STRING	= 's'
HEADER_BINARY	= 'b'
HEADER_CONTROL	= 'c'

STATE_CONNECT			= 1
STATE_STRING_MESSAGE	= 2
STATE_BINARY_MESSAGE	= 3
STATE_DISCONNECT_INTENT	= 4
STATE_DISCONNECT_ACCIDT = 5


ip = "127.0.0.1"
port = 7711

-- entrypoint for WebSocket client connection.


-- setup Disque get-add
local disque = require "disque.disque"

local uuid = require "uuid.uuid"
local time = ngx.now() * 1000
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
	timeout = 10000000,
	max_payload_len = 65535
}

if not ws then
	ngx.log(ngx.ERR, "connection:", connectionId, " failed to new websocket: ", wErr)
	return
end


function connectWebSocket()
	-- start receiving message from context.
	ngx.thread.spawn(contextReceiving)

	ngx.log(ngx.ERR, "connection:", connectionId, " established.")

	-- send connected to gameContext.
	local stringData = HEADER_CONTROL..STATE_CONNECT..connectionId
	addJobCon:addjob(IDENTIFIER_CONTEXT, stringData, 0)

	-- start websocket serving.
	while true do
		local recv_data, typ, err = ws:recv_frame()

		if ws.fatal then
			ngx.log(ngx.ERR, "connection:", connectionId, " closing accidentially. ", err)
			local stringData = HEADER_CONTROL..STATE_DISCONNECT_ACCIDT..connectionId
			addJobCon:addjob(IDENTIFIER_CONTEXT, stringData, 0)
			break
		end

		if not recv_data then
			ngx.log(ngx.ERR, "connection:", connectionId, " received empty data.")
			-- log only. do nothing.
		end

		if typ == "close" then
			ngx.log(ngx.ERR, "connection:", connectionId, " closing intentionally.")
			local stringData = HEADER_CONTROL..STATE_DISCONNECT_INTENT..connectionId
			addJobCon:addjob(IDENTIFIER_CONTEXT, stringData, 0)

			-- start close.
			break
		elseif typ == "ping" then
			-- nothing to do.

		elseif typ == "pong" then
			-- nothing to do.

		elseif typ == "text" then
			-- post message to central.
			local stringData = HEADER_STRING..STATE_STRING_MESSAGE..connectionId..recv_data
			addJobCon:addjob(IDENTIFIER_CONTEXT, stringData, 0)
		elseif typ == "binary" then
			-- post binary data to central.
			local binData = HEADER_BINARY..STATE_BINARY_MESSAGE..connectionId..recv_data
			addJobCon:addjob(IDENTIFIER_CONTEXT, binData, 0)
		end
	end

	ws:send_close()
	ngx.log(ngx.ERR, "connection:", connectionId, " connection closed")
	ngx.exit(200)
end

-- loop for receiving messages from game context.
function contextReceiving ()
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
			end
			-- ngx.log(ngx.ERR, "messageId:", messageId, " ackRes:", ackRes)

			-- connection check
			if not ws:is_connecting() then
				ngx.log(ngx.ERR, "disque, 未解決の、クライアント-ws切断時にすべきこと、、切断情報の通知。connection:", connectionId, " Disque unsubscribed by websocket closed.")
				break
			end

			-- send data to client
			local bytes, err = ws:send_text(sendingData)

			if not bytes then
				ngx.log(ngx.ERR, "disque, 未解決の、送付失敗時にすべきこと。 connection:", connectionId, " failed to send text to client. err:", err)
				break
			end
		end
	end

	if ws:is_connecting() then
		ws:send_close()
	end

	ngx.log(ngx.ERR, "connection:", connectionId, " connection closed by disque error.")
	ngx.exit(200)
end

connectWebSocket()